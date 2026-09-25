using System.Diagnostics;

namespace PhoneBook.SharedKernel.Results;

/// <summary>Outcome of an operation that can fail in an expected way. Failures are values, not exceptions.</summary>
public class Result : IResultBase, IResultFactory<Result>
{
    protected Result(bool isSuccess, Error error)
    {
        Debug.Assert(isSuccess == (error == Error.None), "A success must carry Error.None and a failure a real error.");
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    /// <summary>
    /// Combines results. When every failure is a <see cref="ValidationError"/>, all their field errors are
    /// flattened into one <see cref="ValidationError"/>; otherwise the first non-validation failure wins.
    /// </summary>
    public static Result Combine(params Result[] results)
    {
        var failures = results.Where(r => r.IsFailure).Select(r => r.Error).ToList();
        if (failures.Count == 0)
        {
            return Success();
        }

        var nonValidation = failures.FirstOrDefault(e => e is not ValidationError);
        if (nonValidation is not null)
        {
            return Failure(nonValidation);
        }

        var fieldErrors = failures.Cast<ValidationError>().SelectMany(e => e.Errors).ToList();
        return Failure(new ValidationError(fieldErrors));
    }
}
