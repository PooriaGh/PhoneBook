using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PhoneBook.Identity.Telemetry;

namespace PhoneBook.Identity.RateLimiting;

/// <summary>
/// Per-address rate limiting for the Identity host's credential-accepting endpoints (feature 002, research R-03, R-08).
/// </summary>
/// <remarks>
/// The limiter must run <b>before</b> <c>UseAuthentication</c>: OpenIddict handles and answers token requests
/// (including <c>invalid_client</c>) inside the authentication middleware, so a later limiter would never count
/// credential guessing.
/// </remarks>
internal static class RateLimitingSetup
{
    public static IServiceCollection AddIdentityRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<TokenRateLimitOptions>()
            .BindConfiguration(TokenRateLimitOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(TokenRateLimitOptions.TokenPolicy, PartitionByAddress);
            options.OnRejected = OnRejectedAsync;
        });

        return services;
    }

    private static RateLimitPartition<string> PartitionByAddress(HttpContext context)
    {
        var key = "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        var settings = context.RequestServices.GetRequiredService<IOptionsMonitor<TokenRateLimitOptions>>().CurrentValue;
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

        httpContext.RequestServices.GetRequiredService<IdentityMetrics>().RateLimitRejections.Add(
            1, new KeyValuePair<string, object?>(IdentityMetrics.PolicyTag, TokenRateLimitOptions.TokenPolicy));

        // RFC 6749 defines no token-endpoint error for throttling; temporarily_unavailable is the closest standard code
        // and is documented as an extension in the identity contract (research R-08). HTTP 429 + Retry-After carry the
        // actual signal.
        await httpContext.Response.WriteAsJsonAsync(
            new { error = "temporarily_unavailable", error_description = "Too many requests. Retry later." },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Whole seconds until the window resets, rounded up and never below 1 (FR-009).</summary>
    internal static int RetryAfterSeconds(RateLimitLease lease) =>
        lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : 1;
}
