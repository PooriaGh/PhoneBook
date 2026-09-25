using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PhoneBook.Application.Abstractions.Telemetry;

namespace PhoneBook.Api.Infrastructure.RateLimiting;

/// <summary>
/// Fixed-window rate limiting for the phone book API (feature 002, research R-03, R-08).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Callers are partitioned by the <c>sub</c> claim when authenticated, otherwise by remote address.</item>
/// <item>No queueing: requests over the allowance are rejected at once with <c>429</c>, before authorization, so
/// the <c>429</c> wins over <c>401</c>/<c>403</c> (FR-007).</item>
/// <item>The ProblemDetails <c>errorCode</c> (<c>RateLimit.Exceeded</c>) and <c>traceId</c> are added in one place,
/// by the host's <c>CustomizeProblemDetails</c> status mapping.</item>
/// </list>
/// </remarks>
internal static class RateLimitingSetup
{
    public static IServiceCollection AddPhoneBookRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitOptions>()
            .BindConfiguration(RateLimitOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitOptions.ApiPolicy, PartitionByCaller);
            options.OnRejected = OnRejectedAsync;
        });

        return services;
    }

    private static RateLimitPartition<string> PartitionByCaller(HttpContext context)
    {
        var sub = context.User.Identity?.IsAuthenticated == true ? context.User.FindFirst("sub")?.Value : null;
        var key = sub is not null
            ? "sub:" + sub
            : "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        var settings = context.RequestServices.GetRequiredService<IOptionsMonitor<RateLimitOptions>>().CurrentValue;
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.PermitLimit,
            Window = TimeSpan.FromSeconds(settings.WindowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    }

    private static async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        httpContext.Response.Headers.RetryAfter = RetryAfterSeconds(context.Lease).ToString(CultureInfo.InvariantCulture);

        httpContext.RequestServices.GetRequiredService<PhoneBookMetrics>().RateLimitRejections.Add(
            1, new KeyValuePair<string, object?>(PhoneBookTelemetry.PolicyTag, RateLimitOptions.ApiPolicy));

        await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests",
                Type = "https://tools.ietf.org/html/rfc6585#section-4",
                Detail = "The rate limit for this caller was exceeded. Retry after the time given in Retry-After.",
            },
        }).ConfigureAwait(false);
    }

    /// <summary>Whole seconds until the window resets, rounded up and never below 1 (FR-009).</summary>
    internal static int RetryAfterSeconds(RateLimitLease lease) =>
        lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : 1;
}
