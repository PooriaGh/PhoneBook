using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Domain.Contacts;

/// <summary>
/// First and last name of a contact. Each part is trimmed, not blank and at most 100 characters.
/// Any Unicode is allowed, including the ZWNJ U+200C.
/// </summary>
public sealed record PersonName
{
    public const int MaxLength = 100;

    private PersonName(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
    }

    public string FirstName { get; }

    public string LastName { get; }

    public static Result<PersonName> Create(string? firstName, string? lastName)
    {
        var first = firstName?.Trim() ?? string.Empty;
        var last = lastName?.Trim() ?? string.Empty;

        var validation = Result.Combine(
            ValidatePart(first, "firstName", "PersonName.FirstName", "First name"),
            ValidatePart(last, "lastName", "PersonName.LastName", "Last name"));

        return validation.IsFailure ? validation.Error : new PersonName(first, last);
    }

    internal static PersonName FromPersisted(string firstName, string lastName) => new(firstName, lastName);

    private static Result ValidatePart(string value, string field, string codePrefix, string label)
    {
        if (value.Length == 0)
        {
            return Result.Failure(ValidationError.For(field, $"{codePrefix}.Required", $"{label} is required."));
        }

        return value.Length > MaxLength
            ? Result.Failure(ValidationError.For(
                field, $"{codePrefix}.TooLong", $"{label} must be at most {MaxLength} characters."))
            : Result.Success();
    }
}
