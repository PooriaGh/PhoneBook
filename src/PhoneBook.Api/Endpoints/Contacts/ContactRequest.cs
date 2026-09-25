namespace PhoneBook.Api.Endpoints.Contacts;

/// <summary>Request body for creating or replacing a contact. Values are validated by the domain.</summary>
public sealed record ContactRequest(string? FirstName, string? LastName, string? PhoneNumber, string? Tag);
