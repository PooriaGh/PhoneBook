namespace PhoneBook.SharedKernel.Results;

public interface IResultBase
{
    bool IsSuccess { get; }

    bool IsFailure { get; }

    Error Error { get; }
}
