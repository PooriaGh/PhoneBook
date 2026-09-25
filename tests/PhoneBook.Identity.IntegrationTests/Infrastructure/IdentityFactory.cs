using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PhoneBook.Identity.IntegrationTests.Infrastructure;

/// <summary>Identity host with its own isolated in-memory SQLite store and issuer <c>http://localhost/</c>.</summary>
public sealed class IdentityFactory : WebApplicationFactory<IdentityAssemblyMarker>
{
    public const string Issuer = "http://localhost/";
    public const string ApiOrigin = "https://localhost:7001";

    private readonly string _connectionString = $"Data Source=identity-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Identity", _connectionString);
        builder.UseSetting("Identity:Issuer", Issuer);
        builder.UseSetting("Identity:AllowedCorsOrigins:0", ApiOrigin);
    }
}
