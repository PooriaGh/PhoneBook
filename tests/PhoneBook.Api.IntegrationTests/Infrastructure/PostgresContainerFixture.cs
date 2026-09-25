using Testcontainers.PostgreSql;

namespace PhoneBook.Api.IntegrationTests.Infrastructure;

/// <summary>Starts a single PostgreSQL container per test assembly (xUnit v3 assembly fixture).</summary>
/// <remarks>
/// The database is created with the C locale, so text compares by bytes (Unicode code-point order), exactly like
/// SQLite's BINARY collation. Paging sorts in SQL, so this is what keeps page order identical on both providers
/// (feature 002, research R-02). It is also a deployment requirement for real PostgreSQL databases.
/// </remarks>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("phonebook")
        .WithEnvironment("POSTGRES_INITDB_ARGS", "--locale=C --encoding=UTF8")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
