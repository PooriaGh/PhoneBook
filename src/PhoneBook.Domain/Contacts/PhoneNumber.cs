using System.Text;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Domain.Contacts;

/// <summary>
/// A contact's phone number. Persian and Arabic-Indic digits are converted to ASCII. Then: an optional
/// leading <c>+</c>, then only digits, spaces and <c>-</c>. After spaces and hyphens are removed,
/// 4–15 digits remain. The stored value is <c>+?digits</c>.
/// </summary>
public sealed record PhoneNumber
{
    public const int MinDigits = 4;
    public const int MaxDigits = 15;

    private const string Field = "phoneNumber";

    private PhoneNumber(string value) => Value = value;

    public string Value { get; }

    public static Result<PhoneNumber> Create(string? raw)
    {
        var input = ToAsciiDigits(raw ?? string.Empty).Trim();
        if (input.Length == 0)
        {
            return ValidationError.For(Field, "PhoneNumber.Required", "Phone number is required.");
        }

        var normalized = new StringBuilder(input.Length);
        var digitCount = 0;
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsAsciiDigit(c))
            {
                normalized.Append(c);
                digitCount++;
            }
            else if (c == '+' && i == 0)
            {
                normalized.Append(c);
            }
            else if (c is not (' ' or '-'))
            {
                return ValidationError.For(
                    Field,
                    "PhoneNumber.InvalidCharacters",
                    "Phone number may contain only digits, spaces, '-' and a leading '+'.");
            }
        }

        if (digitCount is < MinDigits or > MaxDigits)
        {
            return ValidationError.For(
                Field,
                "PhoneNumber.InvalidLength",
                $"Phone number must contain between {MinDigits} and {MaxDigits} digits.");
        }

        return new PhoneNumber(normalized.ToString());
    }

    public override string ToString() => Value;

    internal static PhoneNumber FromPersisted(string value) => new(value);

    private static string ToAsciiDigits(string value)
    {
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = chars[i] switch
            {
                >= '۰' and <= '۹' => (char)('0' + (chars[i] - '۰')), // Persian (Extended Arabic-Indic)
                >= '٠' and <= '٩' => (char)('0' + (chars[i] - '٠')), // Arabic-Indic
                _ => chars[i],
            };
        }

        return new string(chars);
    }
}
