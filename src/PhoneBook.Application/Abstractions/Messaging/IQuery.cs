using MediatR;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Application.Abstractions.Messaging;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
