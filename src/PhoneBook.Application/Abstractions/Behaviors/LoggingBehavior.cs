using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Application.Abstractions.Behaviors;

internal sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResultBase
{
    private const long SlowRequestThresholdMs = 500;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        LoggingBehaviorLog.Handling(logger, requestName);
        var stopwatch = Stopwatch.StartNew();

        var response = await next(cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();
        var elapsed = stopwatch.ElapsedMilliseconds;
        if (elapsed > SlowRequestThresholdMs)
        {
            LoggingBehaviorLog.SlowRequest(logger, requestName, elapsed);
        }

        if (response.IsFailure)
        {
            LoggingBehaviorLog.HandledWithFailure(logger, requestName, response.Error.Code, elapsed);
        }
        else
        {
            LoggingBehaviorLog.Handled(logger, requestName, elapsed);
        }

        return response;
    }
}

internal static partial class LoggingBehaviorLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Handling {RequestName}")]
    public static partial void Handling(ILogger logger, string requestName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {RequestName} in {ElapsedMs} ms")]
    public static partial void Handled(ILogger logger, string requestName, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {RequestName} with failure {ErrorCode} in {ElapsedMs} ms")]
    public static partial void HandledWithFailure(ILogger logger, string requestName, string errorCode, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Slow request {RequestName} took {ElapsedMs} ms")]
    public static partial void SlowRequest(ILogger logger, string requestName, long elapsedMs);
}
