using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Domain.Contacts;

public static class ContactErrors
{
    public static readonly Error IdEmpty = ValidationError.For("id", "Contact.Id.Empty", "Contact id must not be empty.");

    public static readonly Error Duplicate = Error.Conflict(
        "Contact.Duplicate", "A contact with this phone number already exists under this tag.");

    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "Contact.ConcurrencyConflict", "The contact was modified by another request. Reload it and try again.");

    public static readonly Error VersionMismatch = Error.PreconditionFailed(
        "Contact.VersionMismatch", "The contact has changed since the version supplied in If-Match.");

    public static Error NotFound(ContactId id) =>
        Error.NotFound("Contact.NotFound", $"Contact '{id}' was not found.");
}
