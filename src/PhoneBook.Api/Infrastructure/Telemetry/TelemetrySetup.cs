using System.Diagnostics;
using System.Reflection;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PhoneBook.Application.Abstractions.Telemetry;

namespace PhoneBook.Api.Infrastructure.Telemetry;

/// <summary>
/// OpenTelemetry traces and metrics for the phone book API (feature 002, research R-04).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Spans: incoming HTTP, outgoing HTTP (discovery/JWKS calls to the identity service), Npgsql, and the custom
/// <c>PhoneBook.Application</c> / <c>PhoneBook.Persistence</c> sources (which also cover SQLite).</item>
/// <item>Personal data (FR-016): <c>url.query</c> (it carries the tag) and <c>client.address</c> are removed;
/// exceptions and request bodies are never recorded.</item>
/// <item>Export (FR-015, FR-017): OTLP only when <c>Telemetry:OtlpEndpoint</c> is set. The exporter batches in the
/// background and drops on failure, so it never blocks requests. The key is read at registration time on purpose:
/// tests attach their own exporters through <c>ConfigureTestServices</c> instead of overriding it.</item>
/// </list>
/// </remarks>
internal static class TelemetrySetup
{
    public static WebApplicationBuilder AddPhoneBookTelemetry(this WebApplicationBuilder builder)
    {
        var version = typeof(TelemetrySetup).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("phonebook-api", serviceVersion: version))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = context => !IsInfrastructurePath(context.Request.Path);
                    options.RecordException = false;
                    options.EnrichWithHttpRequest = static (activity, _) => ScrubPersonalData(activity);
                })
                .AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = false;
                    options.EnrichWithHttpRequestMessage = static (activity, _) => ScrubPersonalData(activity);
                })
                .AddNpgsql()
                .AddSource(PhoneBookTelemetry.ApplicationSourceName, PhoneBookTelemetry.PersistenceSourceName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddMeter(
                    "Microsoft.AspNetCore.Hosting",
                    "Microsoft.AspNetCore.Server.Kestrel",
                    "Microsoft.AspNetCore.RateLimiting",
                    PhoneBookTelemetry.MeterName));

        var endpoint = builder.Configuration[$"{TelemetryOptions.SectionName}:{nameof(TelemetryOptions.OtlpEndpoint)}"];
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            otel.UseOtlpExporter(OtlpExportProtocol.Grpc, new Uri(endpoint, UriKind.Absolute));
        }

        return builder;
    }

    private static bool IsInfrastructurePath(PathString path) =>
        path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase);

    private static void ScrubPersonalData(Activity activity)
    {
        activity.SetTag("url.query", null);
        activity.SetTag("client.address", null);
    }
}
