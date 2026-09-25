using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PhoneBook.Identity.IntegrationTests.Infrastructure;

/// <summary>Identity host with its own isolated in-memory SQLite store and issuer <c>http://localhost/</c>.</summary>
/// <remarks>Unsealed so rate-limit tests can lower <see cref="TokenPermitLimit"/> (feature 002, research R-07).</remarks>
public class IdentityFactory : WebApplicationFactory<IdentityAssemblyMarker>
{
    public const string Issuer = "http://localhost/";
    public const string ApiOrigin = "https://localhost:7001";

    /// <summary>A redirect URI registered for the public client in appsettings.Development.json (feature 002).</summary>
    public const string SwaggerRedirectUri = "https://localhost:7001/swagger/oauth2-redirect.html";

    /// <summary>Effectively unlimited by default; rate-limit tests override it.</summary>
    protected virtual int TokenPermitLimit => 1_000_000;

    private readonly string _connectionString = $"Data Source=identity-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Identity", _connectionString);
        builder.UseSetting("Identity:Issuer", Issuer);
        builder.UseSetting("Identity:AllowedCorsOrigins:0", ApiOrigin);
        builder.UseSetting("RateLimiting:Token:PermitLimit", TokenPermitLimit.ToString(CultureInfo.InvariantCulture));
    }
}
