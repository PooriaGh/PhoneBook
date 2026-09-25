using System.Diagnostics;
using System.Globalization;
using Asp.Versioning;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using PhoneBook.Api;
using PhoneBook.Api.Endpoints;
using PhoneBook.Api.Infrastructure;
using PhoneBook.Api.Infrastructure.Auth;
using PhoneBook.Api.Infrastructure.Swagger;
using PhoneBook.Application;
using PhoneBook.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

// Constitution Principle III: every error response from this host is ProblemDetails with errorCode + traceId.
// /health/* and the Identity host's OAuth endpoints are exempt (research R-17).
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    var extensions = context.ProblemDetails.Extensions;
    extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
    extensions.TryAdd("errorCode", context.ProblemDetails.Status switch
    {
        StatusCodes.Status401Unauthorized => "Auth.Unauthorized",
        StatusCodes.Status403Forbidden => "Auth.Forbidden",
        StatusCodes.Status404NotFound => "General.NotFound",
        StatusCodes.Status405MethodNotAllowed => "General.MethodNotAllowed",
        _ => "General.Error",
    });
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPhoneBookAuth(builder.Configuration);

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1);
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddEndpoints(typeof(ApiAssemblyMarker).Assembly);
builder.Services.AddPhoneBookSwagger();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();
var api = app.MapGroup("api/v{version:apiVersion}").WithApiVersionSet(versionSet);
app.MapEndpoints(api);

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

if (app.Environment.IsDevelopment())
{
    app.UsePhoneBookSwagger();
}

await app.RunAsync().ConfigureAwait(false);

public partial class Program;
