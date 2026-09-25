using Microsoft.EntityFrameworkCore;
using PhoneBook.Domain.Contacts;

namespace PhoneBook.Infrastructure.Persistence.Repositories;

internal sealed class ContactRepository(WriteDbContext dbContext) : IContactRepository
{
    public Task<Contact?> GetByIdAsync(ContactId id, CancellationToken cancellationToken) =>
        dbContext.Contacts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public void Add(Contact contact) => dbContext.Contacts.Add(contact);

    public void Remove(Contact contact) => dbContext.Contacts.Remove(contact);
}
