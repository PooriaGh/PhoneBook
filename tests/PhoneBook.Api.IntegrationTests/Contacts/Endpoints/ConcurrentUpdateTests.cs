using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using static PhoneBook.Api.IntegrationTests.Infrastructure.HttpAssertions;

namespace PhoneBook.Api.IntegrationTests.Contacts.Endpoints;

[Collection(IntegrationTestCollection.Name)]
public sealed class ConcurrentUpdateTests(PhoneBookApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Put_TenConcurrentUpdatesWithSameIfMatch_ExactlyOneWins()
    {
        var seeded = await SeedContactAsync("Ali", "Rezaei", "09121234567", "همکار");
        var uri = ContactsUri($"/{seeded.Id.Value}");

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(i =>
        {
            var request = new HttpRequestMessage(HttpMethod.Put, uri)
            {
                Content = JsonContent.Create(new ContactRequestData($"Name{i}", "Rezaei", "09121234567", "همکار")),
            };
            request.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
            return Client.SendAsync(request, Ct);
        }));

        responses.ShouldNotContain(r => (int)r.StatusCode >= 500);
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.Where(r => r.StatusCode != HttpStatusCode.OK)
            .ShouldAllBe(r => r.StatusCode == HttpStatusCode.PreconditionFailed || r.StatusCode == HttpStatusCode.Conflict);

        var stored = await WithReadDbAsync(db => db.Contacts.SingleAsync(c => c.Id == seeded.Id.Value, Ct));
        stored.Version.ShouldBe(2);
    }
}
