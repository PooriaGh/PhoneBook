using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using static PhoneBook.Api.IntegrationTests.Infrastructure.HttpAssertions;

namespace PhoneBook.Api.IntegrationTests.Contacts.Endpoints;

[Collection(IntegrationTestCollection.Name)]
public sealed class CreateContactConcurrencyTests(PhoneBookApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Post_TwentyIdenticalConcurrentRequests_ExactlyOneCreatedRestDuplicate()
    {
        var request = ContactFaker.Request(tag: "همکار");

        var responses = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => Client.PostAsJsonAsync(ContactsUri(), request, Ct)));

        responses.ShouldNotContain(r => (int)r.StatusCode >= 500);
        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        var conflicts = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();
        conflicts.Count.ShouldBe(19);
        foreach (var conflict in conflicts)
        {
            var body = await conflict.Content.ReadFromJsonAsync<JsonElement>(Ct);
            body.GetProperty("errorCode").GetString().ShouldBe("Contact.Duplicate");
        }

        (await WithReadDbAsync(db => db.Contacts.CountAsync(Ct))).ShouldBe(1);
    }
}
