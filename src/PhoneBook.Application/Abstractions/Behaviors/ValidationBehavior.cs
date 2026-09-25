using FluentValidation;
using MediatR;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Application.Abstractions.Behaviors;

/// <summary>Runs FluentValidation validators and <b>returns</b> a <see cref="ValidationError"/>; never throws.</summary>
internal sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResultBase, IResultFactory<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var validatorList = validators.ToList();
        if (validatorList.Count == 0)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validatorList.Select(v => v.ValidateAsync(context, cancellationToken)))
            .ConfigureAwait(false);

        var fieldErrors = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .Select(f => new FieldError(ToCamelCase(f.PropertyName), f.ErrorCode, f.ErrorMessage))
            .ToList();

        return fieldErrors.Count > 0
            ? TResponse.Failure(new ValidationError(fieldErrors))
            : await next(cancellationToken).ConfigureAwait(false);
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) || char.IsLower(name[0])
            ? name
            : char.ToLowerInvariant(name[0]) + name[1..];
}
