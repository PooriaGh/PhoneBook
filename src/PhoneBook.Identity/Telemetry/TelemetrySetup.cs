using System.Diagnostics;
using System.Reflection;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace PhoneBook.Identity.Telemetry;

/// <summary>
/// OpenTelemetry traces and metrics for the Identity host (feature 002, research R-04).
/// </summary>
/// <remarks>
/// Incoming requests continue the caller's W3C trace, which links the phone book service's discovery and JWKS calls
/// to this host (FR-012). <c>url.query</c> and <c>client.address</c> are removed, and request bodies (client secrets,
/// passwords) are never recorded, because no body enrichment is registered (FR-016). Export is OTLP only when
/// <c>Telemetry:OtlpEndpoint</c> is set (FR-015).
/// </remarks>
internal static class TelemetrySetup
{
    public static WebApplicationBuilder AddIdentityTelemetry(this WebApplicationBuilder builder)
    {
        var version = typeof(TelemetrySetup).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("phonebook-identity", serviceVersion: version))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
                    options.RecordException = false;
                    options.EnrichWithHttpRequest = static (activity, _) => ScrubPersonalData(activity);
                })
                .AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = false;
                    options.EnrichWithHttpRequestMessage = static (activity, _) => ScrubPersonalData(activity);
                }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddMeter("Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.RateLimiting", IdentityMetrics.MeterName));

        var endpoint = builder.Configuration["Telemetry:OtlpEndpoint"];
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            otel.UseOtlpExporter(OtlpExportProtocol.Grpc, new Uri(endpoint, UriKind.Absolute));
        }

        return builder;
    }

    private static void ScrubPersonalData(Activity activity)
    {
        activity.SetTag("url.query", null);
        activity.SetTag("client.address", null);
    }
}
