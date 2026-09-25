using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using PhoneBook.Domain.Contacts;
using static PhoneBook.Api.IntegrationTests.Infrastructure.HttpAssertions;

namespace PhoneBook.Api.IntegrationTests.Contacts.Queries;

/// <summary>
/// Feature 002, US1: paging of the tag search through HTTP, on both providers. The expected order is last name,
/// then first name, by Unicode code point, then id (FR-004, research R-02).
/// </summary>
public abstract class GetContactsByTagPagingTestsBase(IPhoneBookApiFactory factory) : BaseIntegrationTest(factory)
{
    private const string Tag = "همکار";

    // Persian and Latin names, with pairs that differ only in Persian ی (U+06CC) versus Arabic ي (U+064A) and
    // Persian ک (U+06A9) versus Arabic ك (U+0643), plus upper/lower-case Latin.
    private static readonly string[] LastNames =
        ["رضایی", "رضايي", "احمدی", "Smith", "smith", "کریمی", "كريمي", "Ölander"];

    private static readonly string[] FirstNames = ["علی", "مریم", "John", "anna", "ژاله", "Zed"];

    [Fact]
    public async Task Get_250Contacts_ReturnsRequestedPagesInOrder()
    {
        var expected = await SeedAsync(250, Tag);

        var page1 = await GetPageAsync($"?tag={Esc(Tag)}&page=1&pageSize=100");
        ShouldMatchInOrder(page1.Items.Select(c => c.Id), expected.Take(100));
        page1.Page.ShouldBe(1);
        page1.PageSize.ShouldBe(100);
        page1.TotalCount.ShouldBe(250);
        page1.HasNext.ShouldBeTrue();

        var page3 = await GetPageAsync($"?tag={Esc(Tag)}&page=3&pageSize=100");
        ShouldMatchInOrder(page3.Items.Select(c => c.Id), expected.Skip(200));
        page3.HasNext.ShouldBeFalse();

        var defaults = await GetPageAsync($"?tag={Esc(Tag)}");
        ShouldMatchInOrder(defaults.Items.Select(c => c.Id), expected.Take(50));
        defaults.Page.ShouldBe(1);
        defaults.PageSize.ShouldBe(50);
        defaults.HasNext.ShouldBeTrue();
    }

    [Fact]
    public async Task Get_WalkEveryPage_ReturnsEachContactExactlyOnceInOrder()
    {
        var expected = await SeedAsync(250, Tag);

        var walked = new List<Guid>();
        for (var page = 1; ; page++)
        {
            var body = await GetPageAsync($"?tag={Esc(Tag)}&page={page}&pageSize=40");
            walked.AddRange(body.Items.Select(c => c.Id));
            if (!body.HasNext)
            {
                break;
            }
        }

        ShouldMatchInOrder(walked, expected);
    }

    [Fact]
    public async Task Get_PageBeyondTheLast_ReturnsEmptyPageWithTotal()
    {
        await SeedAsync(30, Tag);

        var body = await GetPageAsync($"?tag={Esc(Tag)}&page=5&pageSize=20");

        body.Items.ShouldBeEmpty();
        body.TotalCount.ShouldBe(30);
        body.HasNext.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_HugePageNumber_ReturnsEmptyPageWithoutOverflow()
    {
        await SeedAsync(3, Tag);

        var body = await GetPageAsync($"?tag={Esc(Tag)}&page=1000000&pageSize=200");

        body.Items.ShouldBeEmpty();
        body.TotalCount.ShouldBe(3);
        body.HasNext.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_UnknownTag_ReturnsEmptyPageWithZeroTotal()
    {
        await SeedAsync(3, Tag);

        var body = await GetPageAsync("?tag=unknown&page=1");

        body.Items.ShouldBeEmpty();
        body.TotalCount.ShouldBe(0);
        body.HasNext.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_IdenticalNames_AreOrderedById()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var contact = await SeedContactAsync("سارا", "کریمی", $"0912000{i:D4}", Tag);
            ids.Add(contact.Id.Value);
        }

        var body = await GetPageAsync($"?tag={Esc(Tag)}&pageSize=2");
        var second = await GetPageAsync($"?tag={Esc(Tag)}&page=2&pageSize=2");
        var third = await GetPageAsync($"?tag={Esc(Tag)}&page=3&pageSize=2");

        ShouldMatchInOrder(
            body.Items.Concat(second.Items).Concat(third.Items).Select(c => c.Id),
            ids.OrderBy(IdSortKey, StringComparer.Ordinal));
    }

    /// <summary>Seeds <paramref name="count"/> contacts and returns their ids in the expected page order.</summary>
    private async Task<List<Guid>> SeedAsync(int count, string tag)
    {
        var seeded = new List<Contact>();
        for (var i = 0; i < count; i++)
        {
            var first = FirstNames[i % FirstNames.Length] + (i / 48).ToString(CultureInfo.InvariantCulture);
            var last = LastNames[i % LastNames.Length];
            seeded.Add(await SeedContactAsync(first, last, $"0912{i:D7}", tag));
        }

        return seeded
            .OrderBy(c => c.Name.LastName, StringComparer.Ordinal)
            .ThenBy(c => c.Name.FirstName, StringComparer.Ordinal)
            .ThenBy(c => IdSortKey(c.Id.Value), StringComparer.Ordinal)
            .Select(c => c.Id.Value)
            .ToList();
    }

    /// <summary>
    /// Compares two id sequences and reports only the first difference. Shouldly's full diff of long GUID sequences is
    /// very slow, so it is avoided here.
    /// </summary>
    private static void ShouldMatchInOrder(IEnumerable<Guid> actual, IEnumerable<Guid> expected)
    {
        var a = actual.ToList();
        var e = expected.ToList();
        var firstDifference = Enumerable.Range(0, Math.Min(a.Count, e.Count)).FirstOrDefault(i => a[i] != e[i], -1);
        firstDifference.ShouldBe(-1, $"sequences differ at index {firstDifference}");
        a.Count.ShouldBe(e.Count, "sequence lengths differ");
    }

    // SQLite stores ids as upper-case TEXT and PostgreSQL compares uuid bytes; both match this ordinal order.
    private static string IdSortKey(Guid id) => id.ToString("D").ToUpperInvariant();

    private async Task<PagedContacts> GetPageAsync(string query)
    {
        var response = await Client.GetAsync(ContactsUri(query), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<PagedContacts>(Ct)).ShouldNotBeNull();
    }

    private static string Esc(string value) => Uri.EscapeDataString(value);
}

[Collection(IntegrationTestCollection.Name)]
public sealed class PostgresGetContactsByTagPagingTests(PhoneBookApiFactory factory)
    : GetContactsByTagPagingTestsBase(factory);

[Collection(SqliteIntegrationTestCollection.Name)]
public sealed class SqliteGetContactsByTagPagingTests(SqliteApiFactory factory)
    : GetContactsByTagPagingTestsBase(factory);
