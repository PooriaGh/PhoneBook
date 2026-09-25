using System.Diagnostics;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using PhoneBook.Application.Abstractions.Data;
using PhoneBook.Application.Contacts.GetByTag;

namespace PhoneBook.Api.IntegrationTests.Contacts.Queries;

/// <summary>SC-004: with 10,000 contacts, a tag search returns in under 1 second. Runs on both providers.</summary>
[Trait("Category", "Performance")]
public abstract class GetContactsByTagPerformanceTestsBase(IPhoneBookApiFactory factory, bool textGuids)
    : BaseIntegrationTest(factory)
{
    private const int Total = 10_000;
    private const int Tagged = 500;

    [Fact]
    public async Task SearchByTag_Over10000Contacts_ReturnsUnderOneSecond()
    {
        await BulkInsertAsync();
        await Sender.Send(new GetContactsByTagQuery("perf"), Ct); // warm-up

        var stopwatch = Stopwatch.StartNew();
        var result = await Sender.Send(new GetContactsByTagQuery("perf"), Ct);
        stopwatch.Stop();

        result.Value.Count.ShouldBe(Tagged);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    private async Task BulkInsertAsync()
    {
        const string sql =
            "INSERT INTO contacts (id, first_name, last_name, phone_number, tag, normalized_tag, version, created_at_utc) " +
            "VALUES (@Id, @FirstName, @LastName, @PhoneNumber, @Tag, @NormalizedTag, 1, @CreatedAtUtc)";

        var now = DateTime.UtcNow;
        var rows = Enumerable.Range(0, Total).Select(i =>
        {
            var tag = i < Tagged ? "perf" : $"other{i % 50}";
            var id = Guid.NewGuid();
            return new
            {
                // EF Core stores GUIDs as upper-case TEXT on SQLite; PostgreSQL uses native uuid.
                Id = textGuids ? (object)id.ToString().ToUpperInvariant() : id,
                FirstName = $"First{i}",
                LastName = $"Last{i}",
                PhoneNumber = $"+98912{i:0000000}",
                Tag = tag,
                NormalizedTag = tag.ToUpperInvariant(),
                CreatedAtUtc = now,
            };
        }).ToList();

        await using var scope = Factory.Services.CreateAsyncScope();
        await using var connection = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().CreateConnection();
        await connection.OpenAsync(Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, rows, transaction, cancellationToken: Ct));
        await transaction.CommitAsync(Ct);
    }
}

[Collection(IntegrationTestCollection.Name)]
public sealed class PostgresGetContactsByTagPerformanceTests(PhoneBookApiFactory factory)
    : GetContactsByTagPerformanceTestsBase(factory, textGuids: false);

[Collection(SqliteIntegrationTestCollection.Name)]
public sealed class SqliteGetContactsByTagPerformanceTests(SqliteApiFactory factory)
    : GetContactsByTagPerformanceTestsBase(factory, textGuids: true);
