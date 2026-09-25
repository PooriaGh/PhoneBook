using System.Globalization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Server;
using PhoneBook.Identity;
using PhoneBook.Identity.Data;
using PhoneBook.Identity.Endpoints;
using PhoneBook.Identity.RateLimiting;
using PhoneBook.Identity.Seeding;
using PhoneBook.Identity.Telemetry;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

builder.Services.AddOptions<IdentitySettings>().Bind(builder.Configuration.GetSection(IdentitySettings.SectionName));

// Everything that hosts or tests may override is read at runtime (options), not while registering services.
builder.Services.AddSingleton<InMemorySqliteKeepAlive>();
builder.Services.AddDbContext<IdentityDbContext>((services, options) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    options.UseSqlite(configuration.GetConnectionString(IdentitySettings.ConnectionStringName));
    options.UseOpenIddict();
});

builder.Services.AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<IdentityDbContext>())
    .AddServer(options =>
    {
        options.SetTokenEndpointUris("connect/token");
        options.AllowClientCredentialsFlow();
        options.RegisterScopes(IdentitySeeder.ApiScopes);

        // Ephemeral keys suit an in-memory demo; production would use X.509 certificates.
        options.AddEphemeralEncryptionKey().AddEphemeralSigningKey();

        // The API validates plain signed JWTs from the discovery/JWKS documents.
        options.DisableAccessTokenEncryption();

        var aspNetCore = options.UseAspNetCore().EnableTokenEndpointPassthrough();
        if (builder.Environment.IsDevelopment())
        {
            aspNetCore.DisableTransportSecurityRequirement();
        }
    });

// Fixed issuer (finding U5): the iss claim does not depend on the request host.
builder.Services.AddOptions<OpenIddictServerOptions>()
    .Configure<IOptions<IdentitySettings>>((options, settings) =>
    {
        if (!string.IsNullOrWhiteSpace(settings.Value.Issuer))
        {
            options.Issuer = new Uri(settings.Value.Issuer, UriKind.Absolute);
        }
    });

// CORS for the Swagger UI token request from the API origin (finding U1).
builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>()
    .Configure<IOptions<IdentitySettings>>((options, settings) =>
        options.AddPolicy(IdentitySettings.CorsPolicy, policy => policy
            .WithOrigins([.. settings.Value.AllowedCorsOrigins])
            .AllowAnyHeader()
            .WithMethods(HttpMethods.Post, HttpMethods.Options)));

builder.Services.AddAuthorization();
builder.Services.AddSingleton<IdentityMetrics>();
builder.Services.AddIdentityRateLimiting();
builder.Services.AddHostedService<IdentitySeeder>();
builder.Services.AddHealthChecks().AddDbContextCheck<IdentityDbContext>("database", tags: ["ready"]);

var app = builder.Build();

app.UseSerilogRequestLogging();

// Routing first so endpoint metadata (rate-limit policies) is known; CORS before the limiter so preflights are
// answered at once; the limiter before authentication, because OpenIddict answers token requests (including
// invalid_client) inside the authentication middleware (feature 002, research R-03).
app.UseRouting();
app.UseCors(IdentitySettings.CorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapTokenEndpoint();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
    .DisableRateLimiting();

await app.RunAsync().ConfigureAwait(false);

public partial class Program;
