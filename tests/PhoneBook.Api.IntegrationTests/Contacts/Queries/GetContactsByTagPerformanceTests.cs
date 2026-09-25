using System.Diagnostics;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using PhoneBook.Application.Abstractions.Data;
using PhoneBook.Application.Contacts.GetByTag;

namespace PhoneBook.Api.IntegrationTests.Contacts.Queries;

/// <summary>
/// Feature 002, SC-001: with 100,000 contacts under one tag, any single page is returned in under 500 ms. Page 1 and
/// the last page (the deepest offset) are both measured. Runs on both providers.
/// </summary>
[Trait("Category", "Performance")]
public abstract class GetContactsByTagPerformanceTestsBase(IPhoneBookApiFactory factory, bool textGuids)
    : BaseIntegrationTest(factory)
{
    private const int Tagged = 100_000;
    private const int Others = 10_000;
    private const int PageSize = 50;
    private const int LastPage = Tagged / PageSize;

    [Fact]
    public async Task SearchByTag_PageOf100000TaggedContacts_ReturnsUnder500Ms()
    {
        await BulkInsertAsync();
        await Sender.Send(new GetContactsByTagQuery("perf", 1, PageSize), Ct); // warm-up

        foreach (var page in new[] { 1, LastPage })
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await Sender.Send(new GetContactsByTagQuery("perf", page, PageSize), Ct);
            stopwatch.Stop();

            result.IsSuccess.ShouldBeTrue();
            result.Value.TotalCount.ShouldBe(Tagged);
            result.Value.Items.Count.ShouldBe(PageSize);
            stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(500), $"page {page}");
        }
    }

    /// <summary>
    /// Generates the rows inside the database with one set-based INSERT per provider. This is test seeding code only;
    /// the application's own SQL stays portable (constitution Principle IV).
    /// </summary>
    private async Task BulkInsertAsync()
    {
        // EF Core stores GUIDs as upper-case TEXT on SQLite; PostgreSQL uses native uuid.
        var sql = textGuids
            ? """
              WITH RECURSIVE n(i) AS (SELECT 0 UNION ALL SELECT i + 1 FROM n WHERE i + 1 < @Total)
              INSERT INTO contacts (id, first_name, last_name, phone_number, tag, normalized_tag, version, created_at_utc)
              SELECT upper(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)), 2) || '-'
                           || substr('89AB', 1 + (abs(random()) % 4), 1) || substr(hex(randomblob(2)), 2) || '-'
                           || hex(randomblob(6))),
                     'First' || i, 'Last' || printf('%06d', i), '+98912' || printf('%07d', i),
                     CASE WHEN i < @Tagged THEN 'perf' ELSE 'other' || (i % 50) END,
                     CASE WHEN i < @Tagged THEN 'PERF' ELSE 'OTHER' || (i % 50) END,
                     1, @Now
              FROM n
              """
            : """
              INSERT INTO contacts (id, first_name, last_name, phone_number, tag, normalized_tag, version, created_at_utc)
              SELECT gen_random_uuid(), 'First' || i, 'Last' || lpad(i::text, 6, '0'), '+98912' || lpad(i::text, 7, '0'),
                     CASE WHEN i < @Tagged THEN 'perf' ELSE 'other' || (i % 50) END,
                     CASE WHEN i < @Tagged THEN 'PERF' ELSE 'OTHER' || (i % 50) END,
                     1, @Now
              FROM generate_series(0, @Total - 1) AS s(i)
              """;

        await using var scope = Factory.Services.CreateAsyncScope();
        await using var connection = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().CreateConnection();
        await connection.OpenAsync(Ct);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { Total = Tagged + Others, Tagged, Now = DateTime.UtcNow },
            commandTimeout: 300,
            cancellationToken: Ct));
    }
}

[Collection(IntegrationTestCollection.Name)]
public sealed class PostgresGetContactsByTagPerformanceTests(PhoneBookApiFactory factory)
    : GetContactsByTagPerformanceTestsBase(factory, textGuids: false);

[Collection(SqliteIntegrationTestCollection.Name)]
public sealed class SqliteGetContactsByTagPerformanceTests(SqliteApiFactory factory)
    : GetContactsByTagPerformanceTestsBase(factory, textGuids: true);
