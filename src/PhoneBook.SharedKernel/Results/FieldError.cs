namespace PhoneBook.SharedKernel.Results;

/// <summary>A validation failure bound to a single input field.</summary>
public sealed record FieldError(string Field, string Code, string Description);
