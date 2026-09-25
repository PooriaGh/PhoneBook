namespace PhoneBook.SharedKernel.Results;

/// <summary>Classifies an <see cref="Error"/> so callers (e.g. the API) can map it deterministically.</summary>
public enum ErrorType
{
    None = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    PreconditionFailed = 4,
    Unexpected = 5,
}
