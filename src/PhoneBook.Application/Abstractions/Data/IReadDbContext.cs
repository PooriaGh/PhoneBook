using PhoneBook.Application.Abstractions.Data.ReadModels;

namespace PhoneBook.Application.Abstractions.Data;

/// <summary>Read-only, no-tracking query surface over read models (CQRS read side).</summary>
public interface IReadDbContext
{
    IQueryable<ContactReadModel> Contacts { get; }
}
