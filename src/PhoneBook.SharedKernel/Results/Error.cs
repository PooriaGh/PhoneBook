namespace PhoneBook.SharedKernel.Results;

/// <summary>A typed, expected failure. Returned inside a <see cref="Result"/>; never thrown.</summary>
public record Error(string Code, string Description, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    public static Error PreconditionFailed(string code, string description) =>
        new(code, description, ErrorType.PreconditionFailed);

    public static Error Unexpected(string code, string description) => new(code, description, ErrorType.Unexpected);
}
