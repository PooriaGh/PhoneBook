namespace PhoneBook.Identity.Users;

/// <summary>
/// An end user held in memory (feature 002, data-model §5). <see cref="Id"/> becomes the <c>sub</c> claim; only the
/// password hash is kept.
/// </summary>
public sealed record IdentityUser(Guid Id, string UserName, string PasswordHash, string DisplayName, IReadOnlyList<string> Scopes);
