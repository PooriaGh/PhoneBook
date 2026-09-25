using System.ComponentModel.DataAnnotations;

namespace PhoneBook.Api.Infrastructure.RateLimiting;

/// <summary>
/// The <c>api</c> rate-limit policy, bound from <c>RateLimiting:Api</c> (feature 002, FR-007, FR-011, data-model §2).
/// Read at request time through options, so hosts and tests can change it without code changes.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting:Api";
    public const string ApiPolicy = "api";

    /// <summary>Requests allowed per caller per window.</summary>
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; } = 100;

    /// <summary>Length of the fixed window, in seconds.</summary>
    [Range(1, int.MaxValue)]
    public int WindowSeconds { get; set; } = 60;
}
