using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PhoneBook.Application.Abstractions.Data;

namespace PhoneBook.Api.IntegrationTests.Infrastructure;

/// <summary>
/// API host on the default in-memory SQLite provider (constitution Principle V, finding C1). Each factory
/// instance gets its own isolated in-memory database.
/// </summary>
/// <remarks>Respawn does not support SQLite, so the reset simply deletes all rows.</remarks>
public sealed class SqliteApiFactory : WebApplicationFactory<ApiAssemblyMarker>, IPhoneBookApiFactory
{
    private readonly string _connectionString = $"Data Source=phonebook-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    public async Task ResetDatabaseAsync()
    {
        var factory = Services.GetRequiredService<ISqlConnectionFactory>();
        await using var connection = factory.CreateConnection();
        await connection.ExecuteAsync("DELETE FROM contacts");
    }

    public HttpClient CreateClientWithScopes(string? scopes = null) => CreateClient().WithScopes(scopes);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Database:ConnectionString", _connectionString);
        builder.ConfigureTestServices(services => services.AddTestAuthentication());
    }
}
