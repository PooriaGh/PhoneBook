namespace PhoneBook.Identity.Users;

/// <summary>An end user as configured under <c>Identity:Users</c> (feature 002, data-model §5).</summary>
/// <remarks>The plain-text password exists only in (DEV-ONLY) configuration; it is hashed at start-up.</remarks>
public sealed class IdentityUserOptions
{
    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public IList<string> Scopes { get; init; } = [];
}
