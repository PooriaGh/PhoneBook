using PhoneBook.Identity.Users;

namespace PhoneBook.Identity;

/// <summary>Configuration section <c>Identity</c>.</summary>
public sealed class IdentitySettings
{
    public const string SectionName = "Identity";
    public const string ConnectionStringName = "Identity";
    public const string CorsPolicy = "swagger";

    /// <summary>Fixed token issuer (finding U5). Must equal the API's <c>Auth:Authority</c>.</summary>
    public string? Issuer { get; set; }

    /// <summary>Resource server identifier written to the token <c>aud</c> claim.</summary>
    public string Audience { get; set; } = "phonebook-api";

    /// <summary>Browser origins allowed to call <c>/connect/token</c> (Swagger UI, finding U1).</summary>
    public IList<string> AllowedCorsOrigins { get; init; } = [];

    public IList<IdentityClient> Clients { get; init; } = [];

    /// <summary>End users (feature 002). Loaded only in Development (FR-027); passwords are hashed at start-up.</summary>
    public IList<IdentityUserOptions> Users { get; init; } = [];

    /// <summary>The public first-party client used by the Swagger UI for the authorization-code flow.</summary>
    public SwaggerUiClientOptions SwaggerUi { get; init; } = new();

    /// <summary>Authorization-code lifetime (FR-026). Tests shorten it.</summary>
    public TimeSpan AuthorizationCodeLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Access-token lifetime for every grant (FR-026); equal to the previous default.</summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Sign-in session (cookie) lifetime, not sliding (FR-026). Tests shorten it.</summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromMinutes(15);
}

/// <summary>A confidential client allowed to use the client-credentials grant.</summary>
public sealed class IdentityClient
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public IList<string> Scopes { get; init; } = [];
}
