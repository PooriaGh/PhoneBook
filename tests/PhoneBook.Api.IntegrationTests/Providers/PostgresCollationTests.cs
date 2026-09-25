using Npgsql;
using PhoneBook.Api.IntegrationTests.Infrastructure;

namespace PhoneBook.Api.IntegrationTests.Providers;

/// <summary>
/// Guards the deployment requirement from feature 002 (research R-02): PostgreSQL must compare text by bytes so
/// that SQL paging orders exactly like SQLite's BINARY collation.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class PostgresCollationTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task Database_UsesByteOrderCollation()
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT datcollate FROM pg_database WHERE datname = current_database()", connection);

        var collation = (string?)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        collation.ShouldBe("C");
    }
}
