using FluentValidation;
using FluentValidation.Results;
using PhoneBook.Domain.Contacts;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Application.Contacts;

/// <summary>
/// Shared request validation for contact fields. It delegates to the domain value-object factories, so the
/// validator and the domain can never disagree and error codes are identical (finding D1); every invalid
/// field is reported at once (FR-012).
/// </summary>
internal static class ContactFieldRules
{
    /// <summary>Shape guard on the raw phone input before domain parsing (finding I5).</summary>
    public const int MaxRawPhoneLength = 64;

    public static void AddContactFieldRules<T>(
        this AbstractValidator<T> validator,
        Func<T, string?> firstName,
        Func<T, string?> lastName,
        Func<T, string?> phoneNumber,
        Func<T, string?> tag)
    {
        validator.RuleFor(x => x).Custom((request, context) =>
        {
            var rawPhone = phoneNumber(request);
            var phoneResult = rawPhone is { Length: > MaxRawPhoneLength }
                ? Result.Failure(ValidationError.For(
                    "phoneNumber", "PhoneNumber.TooLong", $"Phone number input must be at most {MaxRawPhoneLength} characters."))
                : PhoneNumber.Create(rawPhone);

            var combined = Result.Combine(
                PersonName.Create(firstName(request), lastName(request)),
                phoneResult,
                Tag.Create(tag(request)));

            if (combined.Error is ValidationError validation)
            {
                foreach (var error in validation.Errors)
                {
                    context.AddFailure(new ValidationFailure(error.Field, error.Description) { ErrorCode = error.Code });
                }
            }
        });
    }
}
