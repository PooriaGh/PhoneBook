using System.Diagnostics.Metrics;

namespace PhoneBook.Identity.Telemetry;

/// <summary>
/// The Identity host's instance of the <c>PhoneBook</c> meter (feature 002, data-model §3). The host does not
/// reference the Application layer, so it keeps its own class with the same meter and instrument names.
/// </summary>
public sealed class IdentityMetrics
{
    public const string MeterName = "PhoneBook";
    public const string PolicyTag = "policy";

    public IdentityMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        RateLimitRejections = meterFactory.Create(MeterName).CreateCounter<long>(
            "phonebook.ratelimit.rejections", description: "Requests rejected by the rate limiter, tagged with the policy.");
    }

    public Counter<long> RateLimitRejections { get; }
}
