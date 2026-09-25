using System.Net;
using System.Net.Http.Json;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using PhoneBook.Application.Contacts;
using static PhoneBook.Api.IntegrationTests.Infrastructure.HttpAssertions;

namespace PhoneBook.Api.IntegrationTests.Contacts.Endpoints;

[Collection(IntegrationTestCollection.Name)]
public sealed class GetContactsByTagEndpointTests(PhoneBookApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Get_ExistingTag_Returns200WithArray()
    {
        await SeedContactAsync("علی", "رضایی", "09121234567", "همکار");
        await SeedContactAsync("مریم", "احمدی", "09121234568", "همکار");

        var response = await Client.GetAsync(TagUri("همکار"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<ContactResponse>>(Ct);
        body.ShouldNotBeNull().Count.ShouldBe(2);
    }

    [Fact]
    public async Task Get_UnknownTag_Returns200EmptyArray()
    {
        var response = await Client.GetAsync(TagUri("unknown"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<ContactResponse>>(Ct)).ShouldNotBeNull().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("?tag=")]
    [InlineData("?tag=%20%20")]
    [InlineData("")]
    public async Task Get_BlankOrMissingTag_Returns400(string query)
    {
        var response = await Client.GetAsync(ContactsUri(query), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "General.Validation");
        problem.ErrorFields().ShouldBe(["tag"]);
    }

    [Fact]
    public async Task Get_TagOf51Characters_Returns400()
    {
        var response = await Client.GetAsync(TagUri(new string('t', 51)), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "General.Validation");
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var anonymous = Factory.CreateClientWithScopes(TestAuthHandler.NoScopes);

        var response = await anonymous.GetAsync(TagUri("همکار"), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.Unauthorized");
    }

    [Fact]
    public async Task Get_ReadOnlyScope_Returns200()
    {
        using var readOnly = Factory.CreateClientWithScopes("phonebook.read");

        var response = await readOnly.GetAsync(TagUri("همکار"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static Uri TagUri(string tag) => ContactsUri($"?tag={Uri.EscapeDataString(tag)}");
}
