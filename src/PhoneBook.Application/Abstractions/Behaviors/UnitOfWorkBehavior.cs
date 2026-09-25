using MediatR;
using PhoneBook.Application.Abstractions.Data;
using PhoneBook.Application.Abstractions.Messaging;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Application.Abstractions.Behaviors;

/// <summary>Commits the Unit of Work after a successful command. Handlers never call SaveChanges themselves.</summary>
internal sealed class UnitOfWorkBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
    where TResponse : IResultBase, IResultFactory<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken).ConfigureAwait(false);
        if (response.IsFailure)
        {
            return response;
        }

        var saveResult = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return saveResult.IsFailure ? TResponse.Failure(saveResult.Error) : response;
    }
}
