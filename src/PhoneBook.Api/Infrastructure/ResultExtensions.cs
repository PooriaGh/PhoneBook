using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Api.Infrastructure;

/// <summary>Maps a failed <see cref="Result"/> to an RFC 9457 ProblemDetails response (data-model §1.4).</summary>
internal static class ResultExtensions
{
    public static IResult ToProblem(this Result result)
    {
        var error = result.Error;
        var extensions = new Dictionary<string, object?> { ["errorCode"] = error.Code };

        if (error.Type == ErrorType.Validation)
        {
            var errors = error is ValidationError validation
                ? validation.Errors
                    .GroupBy(e => e.Field)
                    .ToDictionary(g => g.Key, g => g.Select(e => $"{e.Code}: {e.Description}").ToArray())
                : new Dictionary<string, string[]> { [string.Empty] = [$"{error.Code}: {error.Description}"] };

            return TypedResults.ValidationProblem(
                errors,
                title: "One or more validation errors occurred.",
                type: "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                extensions: extensions);
        }

        var (status, title, type) = error.Type switch
        {
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Not found", "https://tools.ietf.org/html/rfc9110#section-15.5.5"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict", "https://tools.ietf.org/html/rfc9110#section-15.5.10"),
            ErrorType.PreconditionFailed => (StatusCodes.Status412PreconditionFailed, "Precondition failed", "https://tools.ietf.org/html/rfc9110#section-15.5.13"),
            _ => (StatusCodes.Status500InternalServerError, "Server error", "https://tools.ietf.org/html/rfc9110#section-15.6.1"),
        };

        return TypedResults.Problem(
            detail: error.Description,
            statusCode: status,
            title: title,
            type: type,
            extensions: extensions);
    }
}
