using System.Net;
using System.Net.Http.Json;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using PhoneBook.Application.Contacts;
using static PhoneBook.Api.IntegrationTests.Infrastructure.HttpAssertions;

namespace PhoneBook.Api.IntegrationTests.Providers;

/// <summary>
/// Smoke suite for the default in-memory SQLite provider (constitution Principle V, finding C1). Covers code
/// that only runs on SQLite: TEXT GUIDs through EF and Dapper, unique violation 2067, shared-cache locking and
/// the write gate.
/// </summary>
[Collection(SqliteIntegrationTestCollection.Name)]
public sealed class SqliteProviderSmokeTests(SqliteApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task CreateThenGetById_RoundTripsThroughReadDbContext()
    {
        var created = await CreateAsync(new ContactRequestData("علی", "رضایی", "+98 912 123 4567", "همکار"));

        var response = await Client.GetAsync(ContactsUri($"/{created.Id}"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ContactResponse>(Ct)).ShouldBe(created);
    }

    [Fact]
    public async Task SearchByTag_UsesDapperWithTextGuidsAndOrdinalOrder()
    {
        var a = await CreateAsync(new ContactRequestData("علی", "رضایی", "09120000001", "همکار"));
        var b = await CreateAsync(new ContactRequestData("مریم", "احمدی", "09120000002", "همکار"));
        var c = await CreateAsync(new ContactRequestData("حسن", "احمدی", "09120000003", " همکار "));

        var result = await Client.GetFromJsonAsync<List<ContactResponse>>(
            ContactsUri($"?tag={Uri.EscapeDataString("همکار")}"), Ct);

        result.ShouldNotBeNull().Select(r => r.Id).ShouldBe([c.Id, b.Id, a.Id]);
    }

    [Fact]
    public async Task SamePhoneAndTagTwice_Returns409()
    {
        var request = ContactFaker.Request(tag: "همکار");
        await CreateAsync(request);

        var response = await Client.PostAsJsonAsync(ContactsUri(), request, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "Contact.Duplicate");
    }

    [Fact]
    public async Task TwentyIdenticalConcurrentPosts_ExactlyOneCreatedNever500()
    {
        var request = ContactFaker.Request(tag: "همکار");

        var responses = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => Client.PostAsJsonAsync(ContactsUri(), request, Ct)));

        responses.ShouldNotContain(r => (int)r.StatusCode >= 500);
        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(19);
    }

    [Fact]
    public async Task PutWithIfMatch_SucceedsThenStaleReturns412()
    {
        var created = await CreateAsync(ContactFaker.Request());
        var uri = ContactsUri($"/{created.Id}");

        (await PutAsync(uri, ContactFaker.Request(phone: created.PhoneNumber, tag: created.Tag), "\"1\"")).StatusCode
            .ShouldBe(HttpStatusCode.OK);
        var stale = await PutAsync(uri, ContactFaker.Request(phone: created.PhoneNumber, tag: created.Tag), "\"1\"");

        await stale.ShouldBeProblemAsync(HttpStatusCode.PreconditionFailed, "Contact.VersionMismatch");
    }

    [Fact]
    public async Task TenConcurrentPutsWithSameIfMatch_ExactlyOneWinsNever500()
    {
        var created = await CreateAsync(ContactFaker.Request());
        var uri = ContactsUri($"/{created.Id}");

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(i =>
            PutAsync(uri, new ContactRequestData($"Name{i}", created.LastName, created.PhoneNumber, created.Tag), "\"1\"")));

        responses.ShouldNotContain(r => (int)r.StatusCode >= 500);
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteThenDeleteAgain_Returns204Then404()
    {
        var created = await CreateAsync(ContactFaker.Request());
        var uri = ContactsUri($"/{created.Id}");

        (await Client.DeleteAsync(uri, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await (await Client.DeleteAsync(uri, Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "Contact.NotFound");
    }

    private async Task<ContactResponse> CreateAsync(ContactRequestData request)
    {
        var response = await Client.PostAsJsonAsync(ContactsUri(), request, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ContactResponse>(Ct)).ShouldNotBeNull();
    }

    private async Task<HttpResponseMessage> PutAsync(Uri uri, ContactRequestData body, string ifMatch)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, uri) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return await Client.SendAsync(request, Ct);
    }
}
