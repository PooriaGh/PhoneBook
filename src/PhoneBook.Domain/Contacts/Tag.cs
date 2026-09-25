using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Domain.Contacts;

/// <summary>
/// A free-text label on a contact. Trimmed, not blank, at most 50 characters.
/// <c>NormalizedValue = Value.ToUpperInvariant()</c>. Two tags are equal when their <c>NormalizedValue</c> matches.
/// </summary>
/// <remarks>
/// A class (not a record) because equality must use only <see cref="NormalizedValue"/> while the
/// display <see cref="Value"/> is kept as entered.
/// </remarks>
public sealed class Tag : IEquatable<Tag>
{
    public const int MaxLength = 50;

    private const string Field = "tag";

    private Tag(string value)
    {
        Value = value;
        NormalizedValue = value.ToUpperInvariant();
    }

    public string Value { get; }

    public string NormalizedValue { get; }

    public static Result<Tag> Create(string? raw)
    {
        var value = raw?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return ValidationError.For(Field, "Tag.Required", "Tag is required.");
        }

        return value.Length > MaxLength
            ? ValidationError.For(Field, "Tag.TooLong", $"Tag must be at most {MaxLength} characters.")
            : new Tag(value);
    }

    /// <summary>The comparison key used for storage and lookups.</summary>
    public static string Normalize(string raw) => raw.Trim().ToUpperInvariant();

    public bool Equals(Tag? other) =>
        other is not null && string.Equals(NormalizedValue, other.NormalizedValue, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is Tag other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(NormalizedValue);

    public override string ToString() => Value;

    public static bool operator ==(Tag? left, Tag? right) => Equals(left, right);

    public static bool operator !=(Tag? left, Tag? right) => !Equals(left, right);

    internal static Tag FromPersisted(string value) => new(value);
}
