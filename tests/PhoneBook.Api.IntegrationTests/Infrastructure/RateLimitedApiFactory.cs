using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PhoneBook.Application.Abstractions.Data;

namespace PhoneBook.Api.IntegrationTests.Infrastructure;

/// <summary>
/// API host on its own in-memory SQLite database with a tiny rate limit: 5 requests per window (60 s by default,
/// so a window never resets in the middle of a burst; the Retry-After test uses a short window).
/// </summary>
/// <remarks>
/// Not a fixture. Rate-limit counters live in the host, so each test creates and disposes its own instance to start
/// with fresh counters (feature 002, research R-07).
/// </remarks>
public sealed class RateLimitedApiFactory(int windowSeconds = 60)
    : WebApplicationFactory<ApiAssemblyMarker>, IPhoneBookApiFactory
{
    public const int PermitLimit = 5;

    private readonly string _connectionString = $"Data Source=phonebook-ratelimit-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    public async Task ResetDatabaseAsync()
    {
        await using var connection = Services.GetRequiredService<ISqlConnectionFactory>().CreateConnection();
        await connection.ExecuteAsync("DELETE FROM contacts");
    }

    public HttpClient CreateClientWithScopes(string? scopes = null) => CreateClient().WithScopes(scopes);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Database:ConnectionString", _connectionString);
        builder.UseSetting("RateLimiting:Api:PermitLimit", PermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("RateLimiting:Api:WindowSeconds", windowSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.ConfigureTestServices(services => services.AddTestAuthentication());
    }
}
