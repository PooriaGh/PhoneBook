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
    public async Task Get_ExistingTag_Returns200WithPage()
    {
        await SeedContactAsync("علی", "رضایی", "09121234567", "همکار");
        await SeedContactAsync("مریم", "احمدی", "09121234568", "همکار");

        var response = await Client.GetAsync(TagUri("همکار"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedContacts>(Ct);
        body.ShouldNotBeNull().Items.Count.ShouldBe(2);
        body.TotalCount.ShouldBe(2);
        body.HasNext.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_UnknownTag_Returns200EmptyPage()
    {
        var response = await Client.GetAsync(TagUri("unknown"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedContacts>(Ct);
        body.ShouldNotBeNull().Items.ShouldBeEmpty();
        body.TotalCount.ShouldBe(0);
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

    [Theory]
    [InlineData("&page=0", "page", "Paging.Page.Invalid")]
    [InlineData("&page=-1", "page", "Paging.Page.Invalid")]
    [InlineData("&page=abc", "page", "Paging.Page.Invalid")]
    [InlineData("&page=99999999999", "page", "Paging.Page.Invalid")]
    [InlineData("&pageSize=0", "pageSize", "Paging.PageSize.Invalid")]
    [InlineData("&pageSize=201", "pageSize", "Paging.PageSize.Invalid")]
    [InlineData("&pageSize=x", "pageSize", "Paging.PageSize.Invalid")]
    public async Task Get_InvalidPagingValue_Returns400WithFieldCode(string paging, string field, string code)
    {
        var response = await Client.GetAsync(ContactsUri($"?tag=work{paging}"), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "General.Validation");
        problem.ErrorFields().ShouldBe([field]);
        problem.GetProperty("errors").GetProperty(field).GetRawText().ShouldContain(code);
    }

    [Fact]
    public async Task Get_EmptyPagingValues_UseDefaults()
    {
        var response = await Client.GetAsync(ContactsUri("?tag=work&page=&pageSize="), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<PagedContacts>(Ct)).ShouldNotBeNull();
        body.Page.ShouldBe(1);
        body.PageSize.ShouldBe(50);
    }

    [Fact]
    public async Task Get_BlankTagAndInvalidPage_ReportsBothFields()
    {
        var response = await Client.GetAsync(ContactsUri("?tag=&page=0"), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "General.Validation");
        problem.ErrorFields().Order(StringComparer.Ordinal).ShouldBe(["page", "tag"]);
    }

    private static Uri TagUri(string tag) => ContactsUri($"?tag={Uri.EscapeDataString(tag)}");
}
