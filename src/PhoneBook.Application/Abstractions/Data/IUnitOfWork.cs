using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Application.Abstractions.Data;

/// <summary>Commits the write side. Persistence conflicts come back as failed results, not exceptions.</summary>
public interface IUnitOfWork
{
    Task<Result> SaveChangesAsync(CancellationToken cancellationToken);
}
