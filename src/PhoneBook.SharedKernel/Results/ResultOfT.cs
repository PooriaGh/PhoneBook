namespace PhoneBook.SharedKernel.Results;

/// <summary>Outcome carrying a value on success.</summary>
public class Result<T> : Result, IResultFactory<Result<T>>
{
    private readonly T _value;

    private Result(T value)
        : base(true, Error.None) => _value = value;

    private Result(Error error)
        : base(false, error) => _value = default!;

    /// <summary>
    /// The success value. Reading it on a failed result returns <c>default</c>;
    /// check <see cref="Result.IsSuccess"/> first.
    /// </summary>
    public T Value => _value;

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        IsSuccess ? onSuccess(_value) : onFailure(Error);
}
