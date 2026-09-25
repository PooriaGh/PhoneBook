using System.Net;
using System.Net.Http.Json;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using PhoneBook.Application.Contacts;
using static PhoneBook.Api.IntegrationTests.Infrastructure.HttpAssertions;

namespace PhoneBook.Api.IntegrationTests.Contacts.Endpoints;

[Collection(IntegrationTestCollection.Name)]
public sealed class CreateContactEndpointTests(PhoneBookApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Post_ValidContact_Returns201WithLocationAndETag()
    {
        var request = new ContactRequestData("علی", "رضایی", "+98 912 123 4567", "شماره همکارم در ترابرنت");

        var response = await Client.PostAsJsonAsync(ContactsUri(), request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.ETag?.Tag.ShouldBe("\"1\"");
        var body = await response.Content.ReadFromJsonAsync<ContactResponse>(Ct);
        body.ShouldNotBeNull();
        body.ShouldSatisfyAllConditions(
            b => b.Id.ShouldNotBe(Guid.Empty),
            b => b.FirstName.ShouldBe("علی"),
            b => b.LastName.ShouldBe("رضایی"),
            b => b.PhoneNumber.ShouldBe("+989121234567"),
            b => b.Tag.ShouldBe("شماره همکارم در ترابرنت"),
            b => b.Version.ShouldBe(1));

        var location = response.Headers.Location;
        location.ShouldNotBeNull();
        location.ToString().ShouldEndWith($"{ContactsPath}/{body.Id}", Case.Insensitive);

        var fetched = await Client.GetAsync(location, Ct);
        fetched.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await fetched.Content.ReadFromJsonAsync<ContactResponse>(Ct)).ShouldBe(body);
    }

    [Fact]
    public async Task Post_BlankLastNameAndInvalidPhone_Returns400WithBothFields()
    {
        var response = await Client.PostAsJsonAsync(ContactsUri(), new ContactRequestData("Ali", "", "abc", "همکار"), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "General.Validation");
        problem.ErrorFields().ShouldBe(["lastName", "phoneNumber"], ignoreOrder: true);
    }

    [Fact]
    public async Task Post_DuplicatePhoneAndTag_Returns409()
    {
        var request = ContactFaker.Request(tag: "همکار");
        (await Client.PostAsJsonAsync(ContactsUri(), request, Ct)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await Client.PostAsJsonAsync(ContactsUri(), request with { FirstName = "Other" }, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "Contact.Duplicate");
    }

    [Fact]
    public async Task Post_Anonymous_Returns401Problem()
    {
        using var anonymous = Factory.CreateClientWithScopes(TestAuthHandler.NoScopes);

        var response = await anonymous.PostAsJsonAsync(ContactsUri(), ContactFaker.Request(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.Unauthorized");
    }

    [Fact]
    public async Task Post_ReadOnlyScope_Returns403Problem()
    {
        using var readOnly = Factory.CreateClientWithScopes("phonebook.read");

        var response = await readOnly.PostAsJsonAsync(ContactsUri(), ContactFaker.Request(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Auth.Forbidden");
    }
}
