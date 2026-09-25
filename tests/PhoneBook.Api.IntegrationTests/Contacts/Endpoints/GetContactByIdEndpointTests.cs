using System.Net;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using static PhoneBook.Api.IntegrationTests.Infrastructure.HttpAssertions;

namespace PhoneBook.Api.IntegrationTests.Contacts.Endpoints;

[Collection(IntegrationTestCollection.Name)]
public sealed class GetContactByIdEndpointTests(PhoneBookApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Get_ExistingContact_Returns200WithETag()
    {
        var seeded = await SeedContactAsync("Ali", "Rezaei", "09121234567", "work");

        var response = await Client.GetAsync(ContactsUri($"/{seeded.Id.Value}"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag?.Tag.ShouldBe("\"1\"");
    }

    [Fact]
    public async Task Get_UnknownGuid_Returns404Problem()
    {
        var response = await Client.GetAsync(ContactsUri($"/{Guid.NewGuid()}"), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "Contact.NotFound");
    }

    [Fact]
    public async Task Get_MalformedId_Returns400InvalidId()
    {
        var response = await Client.GetAsync(ContactsUri("/not-a-guid"), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Contact.Id.Invalid");
        problem.ErrorFields().ShouldBe(["id"]);
    }

    [Fact]
    public async Task Get_MalformedIdAnonymous_Returns401Problem()
    {
        using var anonymous = Factory.CreateClientWithScopes(TestAuthHandler.NoScopes);

        var response = await anonymous.GetAsync(ContactsUri("/not-a-guid"), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.Unauthorized");
    }
}
