using MediatR;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Application.Abstractions.Messaging;

public interface ICommand : IRequest<Result>, IBaseCommand;

public interface ICommand<TResponse> : IRequest<Result<TResponse>>, IBaseCommand;
