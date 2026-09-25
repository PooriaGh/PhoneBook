using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using static PhoneBook.Api.IntegrationTests.Infrastructure.HttpAssertions;

namespace PhoneBook.Api.IntegrationTests.Contacts.Endpoints;

/// <summary>
/// SC-005: 100 concurrent add/edit/delete/search requests leave the phone book consistent. Runs on both
/// providers. 40 contacts are seeded, because 30 distinct updates plus 10 distinct deletes need 40 targets.
/// </summary>
public abstract class MixedConcurrencyTestsBase(IPhoneBookApiFactory factory) : BaseIntegrationTest(factory)
{
    private const int Seeded = 40;
    private const int Creates = 40;
    private const int Updates = 30;
    private const int Deletes = 10;
    private const int Searches = 20;

    [Fact]
    public async Task HundredConcurrentMixedRequests_LeaveDataConsistent()
    {
        var seeded = new List<Domain.Contacts.Contact>();
        for (var i = 0; i < Seeded; i++)
        {
            seeded.Add(await SeedContactAsync($"Seed{i}", "Contact", Phone(1_000 + i), "load"));
        }

        var updateTargets = seeded.Take(Updates).ToList();
        var deleteTargets = seeded.Skip(Updates).Take(Deletes).ToList();

        var requests = new List<Task<HttpResponseMessage>>();
        requests.AddRange(Enumerable.Range(0, Creates).Select(i =>
            Client.PostAsJsonAsync(ContactsUri(), new ContactRequestData($"New{i}", "Contact", Phone(5_000 + i), "load"), Ct)));
        requests.AddRange(updateTargets.Select(c =>
        {
            var request = new HttpRequestMessage(HttpMethod.Put, ContactsUri($"/{c.Id.Value}"))
            {
                Content = JsonContent.Create(new ContactRequestData("Updated", "Contact", c.Phone.Value, "load")),
            };
            request.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
            return Client.SendAsync(request, Ct);
        }));
        requests.AddRange(deleteTargets.Select(c => Client.DeleteAsync(ContactsUri($"/{c.Id.Value}"), Ct)));
        requests.AddRange(Enumerable.Range(0, Searches).Select(_ => Client.GetAsync(ContactsUri("?tag=load"), Ct)));

        var responses = await Task.WhenAll(requests);

        responses.ShouldNotContain(r => (int)r.StatusCode >= 500);
        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(Creates);
        responses.Count(r => r.StatusCode == HttpStatusCode.NoContent).ShouldBe(Deletes);

        var rows = await WithReadDbAsync(db => db.Contacts.ToListAsync(Ct));
        rows.Count.ShouldBe(Seeded + Creates - Deletes);
        var updatedIds = updateTargets.Select(c => c.Id.Value).ToHashSet();
        rows.Where(r => updatedIds.Contains(r.Id)).ShouldAllBe(r => r.Version == 2 && r.FirstName == "Updated");
        rows.GroupBy(r => (r.PhoneNumber, r.NormalizedTag)).ShouldAllBe(g => g.Count() == 1);
    }

    private static string Phone(int n) => "0912" + n.ToString("0000000", CultureInfo.InvariantCulture);
}

[Collection(IntegrationTestCollection.Name)]
public sealed class PostgresMixedConcurrencyTests(PhoneBookApiFactory factory) : MixedConcurrencyTestsBase(factory);

[Collection(SqliteIntegrationTestCollection.Name)]
public sealed class SqliteMixedConcurrencyTests(SqliteApiFactory factory) : MixedConcurrencyTestsBase(factory);
