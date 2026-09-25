using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Npgsql;
using Respawn;

namespace PhoneBook.Api.IntegrationTests.Infrastructure;

/// <summary>API host on PostgreSQL (Testcontainers), reset between tests with Respawn.</summary>
public sealed class PhoneBookApiFactory(PostgresContainerFixture postgres)
    : WebApplicationFactory<ApiAssemblyMarker>, IPhoneBookApiFactory
{
    private readonly SemaphoreSlim _respawnerLock = new(1, 1);
    private Respawner? _respawner;

    public async Task ResetDatabaseAsync()
    {
        _ = Services; // Starts the host, which creates the schema.

        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();

        await _respawnerLock.WaitAsync();
        try
        {
            _respawner ??= await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],
            });
        }
        finally
        {
            _respawnerLock.Release();
        }

        await _respawner.ResetAsync(connection);
    }

    public HttpClient CreateClientWithScopes(string? scopes = null) => CreateClient().WithScopes(scopes);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "Postgres");
        builder.UseSetting("Database:ConnectionString", postgres.ConnectionString);

        // Effectively unlimited, so load tests never hit the rate limiter (feature 002, research R-03).
        builder.UseSetting("RateLimiting:Api:PermitLimit", "1000000");
        builder.ConfigureTestServices(services => services.AddTestAuthentication());
    }

    public override async ValueTask DisposeAsync()
    {
        _respawnerLock.Dispose();
        await base.DisposeAsync();
    }
}
