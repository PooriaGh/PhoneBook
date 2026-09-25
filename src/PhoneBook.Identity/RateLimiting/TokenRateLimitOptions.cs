using System.ComponentModel.DataAnnotations;

namespace PhoneBook.Identity.RateLimiting;

/// <summary>
/// The <c>token</c> rate-limit policy for credential-accepting endpoints, bound from <c>RateLimiting:Token</c>
/// (feature 002, FR-008, FR-011; constitution v1.0.2 Principle VI).
/// </summary>
public sealed class TokenRateLimitOptions
{
    public const string SectionName = "RateLimiting:Token";
    public const string TokenPolicy = "token";

    /// <summary>Requests allowed per source address per window.</summary>
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; } = 10;

    /// <summary>Length of the fixed window, in seconds.</summary>
    [Range(1, int.MaxValue)]
    public int WindowSeconds { get; set; } = 60;
}
