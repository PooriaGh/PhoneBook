using System.Net;
using System.Net.Http.Json;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using PhoneBook.Application.Contacts;
using static PhoneBook.Api.IntegrationTests.Infrastructure.HttpAssertions;

namespace PhoneBook.Api.IntegrationTests.Contacts.Endpoints;

[Collection(IntegrationTestCollection.Name)]
public sealed class DeleteContactEndpointTests(PhoneBookApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Delete_Existing_Returns204ThenRepeatReturns404()
    {
        var seeded = await SeedContactAsync("Ali", "Rezaei", "09121234567", "همکار");
        var uri = ContactsUri($"/{seeded.Id.Value}");

        var first = await Client.DeleteAsync(uri, Ct);
        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await first.Content.ReadAsByteArrayAsync(Ct)).ShouldBeEmpty();

        var second = await Client.DeleteAsync(uri, Ct);
        await second.ShouldBeProblemAsync(HttpStatusCode.NotFound, "Contact.NotFound");
    }

    [Fact]
    public async Task Delete_MalformedId_Returns400()
    {
        var response = await Client.DeleteAsync(ContactsUri("/not-a-guid"), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Contact.Id.Invalid");
    }

    [Fact]
    public async Task Delete_StaleIfMatch_Returns412()
    {
        var seeded = await SeedContactAsync("Ali", "Rezaei", "09121234567", "همکار");
        using var request = new HttpRequestMessage(HttpMethod.Delete, ContactsUri($"/{seeded.Id.Value}"));
        request.Headers.TryAddWithoutValidation("If-Match", "\"7\"");

        var response = await Client.SendAsync(request, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.PreconditionFailed, "Contact.VersionMismatch");
    }

    [Fact]
    public async Task Delete_ReadOnlyScope_Returns403()
    {
        var seeded = await SeedContactAsync("Ali", "Rezaei", "09121234567", "همکار");
        using var readOnly = Factory.CreateClientWithScopes("phonebook.read");

        var response = await readOnly.DeleteAsync(ContactsUri($"/{seeded.Id.Value}"), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Auth.Forbidden");
    }

    [Fact]
    public async Task Delete_ThenSearchByTag_ContactIsGone()
    {
        var kept = await SeedContactAsync("Maryam", "Ahmadi", "09121111111", "همکار");
        var removed = await SeedContactAsync("Ali", "Rezaei", "09122222222", "همکار");

        (await Client.DeleteAsync(ContactsUri($"/{removed.Id.Value}"), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var remaining = await Client.GetFromJsonAsync<PagedContacts>(
            ContactsUri($"?tag={Uri.EscapeDataString("همکار")}"), Ct);
        remaining.ShouldNotBeNull().Items.ShouldHaveSingleItem().Id.ShouldBe(kept.Id.Value);
    }
}
