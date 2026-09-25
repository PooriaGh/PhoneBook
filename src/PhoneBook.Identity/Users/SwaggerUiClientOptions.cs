namespace PhoneBook.Identity.Users;

/// <summary>The public Swagger UI client for the authorization-code flow (feature 002, data-model §5).</summary>
public sealed class SwaggerUiClientOptions
{
    public string ClientId { get; set; } = "phonebook-swagger-ui";

    public string DisplayName { get; set; } = "PhoneBook Swagger UI (end-user sign-in)";

    /// <summary>Registered redirect URIs; OpenIddict compares them by exact string match (FR-024).</summary>
    public IList<string> RedirectUris { get; init; } = [];
}
