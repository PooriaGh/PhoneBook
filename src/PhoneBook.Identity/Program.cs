using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
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
using PhoneBook.Identity.Users;
using static OpenIddict.Abstractions.OpenIddictConstants;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// preserveStaticLogger: each host keeps its own logger instead of replacing the global Log.Logger, so hosts that
// share a process (integration tests) never write into each other's sinks (feature 002, FR-016 log scans).
builder.Host.UseSerilog(
    (context, services, logger) => logger
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture),
    preserveStaticLogger: true);

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

        // End-user sign-in (feature 002, research R-05, R-08): authorization code with mandatory PKCE. Token and
        // authorization storage stay enabled (the default), so a replayed code also revokes its tokens (FR-019).
        options.SetAuthorizationEndpointUris("connect/authorize");
        options.AllowAuthorizationCodeFlow();
        options.RequireProofKeyForCodeExchange();
        options.RegisterScopes(IdentitySeeder.ApiScopes);

        // Ephemeral keys suit an in-memory demo; production would use X.509 certificates.
        options.AddEphemeralEncryptionKey().AddEphemeralSigningKey();

        // The API validates plain signed JWTs from the discovery/JWKS documents.
        options.DisableAccessTokenEncryption();

        var aspNetCore = options.UseAspNetCore()
            .EnableTokenEndpointPassthrough()
            .EnableAuthorizationEndpointPassthrough();
        if (builder.Environment.IsDevelopment())
        {
            aspNetCore.DisableTransportSecurityRequirement();
        }
    });

// Fixed issuer (finding U5): the iss claim does not depend on the request host. Lifetimes and the S256-only PKCE
// rule are applied here too, read from settings at runtime so tests can change them (feature 002, FR-018, FR-026).
builder.Services.AddOptions<OpenIddictServerOptions>()
    .Configure<IOptions<IdentitySettings>>((options, settings) =>
    {
        if (!string.IsNullOrWhiteSpace(settings.Value.Issuer))
        {
            options.Issuer = new Uri(settings.Value.Issuer, UriKind.Absolute);
        }

        options.AuthorizationCodeLifetime = settings.Value.AuthorizationCodeLifetime;
        options.AccessTokenLifetime = settings.Value.AccessTokenLifetime;
        options.CodeChallengeMethods.Clear();
        options.CodeChallengeMethods.Add(CodeChallengeMethods.Sha256);
    });

// The sign-in session (FR-025, FR-026): HttpOnly, SameSite=Lax, Secure over HTTPS, fixed lifetime (no sliding).
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/account/login";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = false;
    });
builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<IOptions<IdentitySettings>>((options, settings) => options.ExpireTimeSpan = settings.Value.SessionLifetime);
builder.Services.AddAntiforgery();
builder.Services.AddSingleton<InMemoryUserStore>();

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
builder.AddIdentityTelemetry();
builder.Services.AddHostedService<IdentitySeeder>();
builder.Services.AddHealthChecks().AddDbContextCheck<IdentityDbContext>("database", tags: ["ready"]);

var app = builder.Build();

// The host's own Serilog logger (see preserveStaticLogger above), not the static Log.Logger.
app.UseSerilogRequestLogging(options => options.Logger = app.Services.GetRequiredService<Serilog.ILogger>());

// Routing first so endpoint metadata (rate-limit policies) is known; CORS before the limiter so preflights are
// answered at once; the limiter before authentication, because OpenIddict answers token requests (including
// invalid_client) inside the authentication middleware (feature 002, research R-03).
app.UseRouting();
app.UseCors(IdentitySettings.CorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapTokenEndpoint();
app.MapAuthorizeEndpoint();
app.MapAccountEndpoints();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
    .DisableRateLimiting();

await app.RunAsync().ConfigureAwait(false);

public partial class Program;
