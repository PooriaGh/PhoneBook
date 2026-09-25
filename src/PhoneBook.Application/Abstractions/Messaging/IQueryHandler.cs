using MediatR;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Application.Abstractions.Messaging;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
