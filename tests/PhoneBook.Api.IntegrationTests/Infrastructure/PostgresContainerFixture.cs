using Testcontainers.PostgreSql;

namespace PhoneBook.Api.IntegrationTests.Infrastructure;

/// <summary>Starts a single PostgreSQL container per test assembly (xUnit v3 assembly fixture).</summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("phonebook")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
