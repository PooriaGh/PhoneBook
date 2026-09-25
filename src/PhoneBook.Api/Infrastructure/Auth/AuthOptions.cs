using System.ComponentModel.DataAnnotations;

namespace PhoneBook.Api.Infrastructure.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Token issuer and discovery base URL, reachable server-to-server. Must equal Identity:Issuer.</summary>
    [Required]
    public string Authority { get; set; } = string.Empty;

    /// <summary>Browser-reachable Identity base URL used for Swagger's token URL (finding U5). Defaults to <see cref="Authority"/>.</summary>
    public string? PublicAuthority { get; set; }

    [Required]
    public string Audience { get; set; } = "phonebook-api";

    public bool RequireHttpsMetadata { get; set; } = true;

    public string EffectivePublicAuthority => string.IsNullOrWhiteSpace(PublicAuthority) ? Authority : PublicAuthority;
}
