using MediatR;
using Microsoft.Extensions.Logging;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Application.Abstractions.Behaviors;

/// <summary>
/// First safety net (constitution Principle III): any exception escaping a handler becomes an
/// <c>Unexpected</c> failure result instead of propagating.
/// </summary>
/// <remarks>
/// <see cref="OperationCanceledException"/> is re-thrown when the caller's token was cancelled. This is the one
/// documented exception to constitution Principle II: cancellation is not a business failure.
/// </remarks>
internal sealed class UnhandledExceptionBehavior<TRequest, TResponse>(
    ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResultBase, IResultFactory<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
#pragma warning disable CA1031 // Catch general exception types — this behaviour exists to do exactly that.
        try
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception while handling {RequestName}", typeof(TRequest).Name);
            return TResponse.Failure(Error.Unexpected("General.Unexpected", "An unexpected error occurred."));
        }
#pragma warning restore CA1031
    }
}
