using System.Net;
using System.Net.Http.Json;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using PhoneBook.Application.Contacts;
using static PhoneBook.Api.IntegrationTests.Infrastructure.HttpAssertions;

namespace PhoneBook.Api.IntegrationTests.Contacts.Endpoints;

[Collection(IntegrationTestCollection.Name)]
public sealed class UpdateContactEndpointTests(PhoneBookApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Put_ValidBody_Returns200WithNewETag()
    {
        var seeded = await SeedContactAsync("Ali", "Rezaei", "09121234567", "همکار");

        var response = await Client.PutAsJsonAsync(
            ContactsUri($"/{seeded.Id.Value}"), new ContactRequestData("Reza", "Alavi", "09121234567", "دوست"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag?.Tag.ShouldBe("\"2\"");
        var body = await response.Content.ReadFromJsonAsync<ContactResponse>(Ct);
        body.ShouldNotBeNull().ShouldSatisfyAllConditions(
            b => b.Id.ShouldBe(seeded.Id.Value),
            b => b.FirstName.ShouldBe("Reza"),
            b => b.Tag.ShouldBe("دوست"),
            b => b.Version.ShouldBe(2));
    }

    [Fact]
    public async Task Put_UnknownId_Returns404()
    {
        var response = await Client.PutAsJsonAsync(ContactsUri($"/{Guid.NewGuid()}"), ContactFaker.Request(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "Contact.NotFound");
    }

    [Fact]
    public async Task Put_BlankLastName_Returns400()
    {
        var seeded = await SeedContactAsync("Ali", "Rezaei", "09121234567", "همکار");

        var response = await Client.PutAsJsonAsync(
            ContactsUri($"/{seeded.Id.Value}"), new ContactRequestData("Ali", "", "09121234567", "همکار"), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "General.Validation");
        problem.ErrorFields().ShouldBe(["lastName"]);
    }

    [Fact]
    public async Task Put_MalformedId_Returns400()
    {
        var response = await Client.PutAsJsonAsync(ContactsUri("/not-a-guid"), ContactFaker.Request(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Contact.Id.Invalid");
    }

    [Fact]
    public async Task Put_StaleIfMatch_Returns412()
    {
        var seeded = await SeedContactAsync("Ali", "Rezaei", "09121234567", "همکار");
        var uri = ContactsUri($"/{seeded.Id.Value}");
        (await PutWithIfMatchAsync(uri, new ContactRequestData("Reza", "Rezaei", "09121234567", "همکار"), "\"1\""))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await PutWithIfMatchAsync(uri, new ContactRequestData("Hasan", "Rezaei", "09121234567", "همکار"), "\"1\"");

        await response.ShouldBeProblemAsync(HttpStatusCode.PreconditionFailed, "Contact.VersionMismatch");
    }

    [Fact]
    public async Task Put_MalformedIfMatch_Returns400()
    {
        var seeded = await SeedContactAsync("Ali", "Rezaei", "09121234567", "همکار");

        var response = await PutWithIfMatchAsync(
            ContactsUri($"/{seeded.Id.Value}"), new ContactRequestData("Reza", "Rezaei", "09121234567", "همکار"), "\"abc\"");

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "General.Validation");
    }

    [Fact]
    public async Task Put_ToAnotherContactsPhoneAndTag_Returns409()
    {
        await SeedContactAsync("Ali", "Rezaei", "09121111111", "همکار");
        var other = await SeedContactAsync("Reza", "Alavi", "09122222222", "دوست");

        var response = await Client.PutAsJsonAsync(
            ContactsUri($"/{other.Id.Value}"), new ContactRequestData("Reza", "Alavi", "09121111111", "همکار"), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "Contact.Duplicate");
    }

    [Fact]
    public async Task Put_ReadOnlyScope_Returns403()
    {
        var seeded = await SeedContactAsync("Ali", "Rezaei", "09121234567", "همکار");
        using var readOnly = Factory.CreateClientWithScopes("phonebook.read");

        var response = await readOnly.PutAsJsonAsync(ContactsUri($"/{seeded.Id.Value}"), ContactFaker.Request(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Auth.Forbidden");
    }

    private async Task<HttpResponseMessage> PutWithIfMatchAsync(Uri uri, ContactRequestData body, string ifMatch)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, uri) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return await Client.SendAsync(request, Ct);
    }
}
