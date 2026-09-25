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
}

/// <summary>A confidential client allowed to use the client-credentials grant.</summary>
public sealed class IdentityClient
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public IList<string> Scopes { get; init; } = [];
}
