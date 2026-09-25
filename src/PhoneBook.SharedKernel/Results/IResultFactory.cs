namespace PhoneBook.SharedKernel.Results;

/// <summary>
/// Lets generic code (e.g. MediatR pipeline behaviours) create a failed <typeparamref name="TSelf"/>
/// without reflection.
/// </summary>
public interface IResultFactory<TSelf>
    where TSelf : IResultFactory<TSelf>
{
    static abstract TSelf Failure(Error error);
}
