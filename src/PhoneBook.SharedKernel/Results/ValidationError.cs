namespace PhoneBook.SharedKernel.Results;

/// <summary>One or more per-field validation failures (FR-012).</summary>
public sealed record ValidationError(IReadOnlyList<FieldError> Errors)
    : Error("General.Validation", "One or more validation errors occurred.", ErrorType.Validation)
{
    public static ValidationError For(string field, string code, string description) =>
        new([new FieldError(field, code, description)]);
}
