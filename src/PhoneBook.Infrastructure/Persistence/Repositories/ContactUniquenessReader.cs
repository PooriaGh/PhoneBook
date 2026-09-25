using Microsoft.EntityFrameworkCore;
using PhoneBook.Domain.Contacts;
using PhoneBook.Domain.Contacts.Services;

namespace PhoneBook.Infrastructure.Persistence.Repositories;

/// <summary>Implements the domain port behind <see cref="ContactDuplicateChecker"/> (FR-018).</summary>
internal sealed class ContactUniquenessReader(WriteDbContext dbContext) : IContactUniquenessReader
{
    public async Task<bool> ExistsAsync(
        PhoneNumber phone, Tag tag, ContactId? excluding, CancellationToken cancellationToken)
    {
        // Unsaved additions in the same unit of work also count as duplicates.
        var pendingDuplicate = dbContext.ChangeTracker.Entries<Contact>()
            .Any(e => e.State == EntityState.Added
                      && e.Entity.Phone == phone
                      && e.Entity.Tag == tag
                      && e.Entity.Id != excluding);
        if (pendingDuplicate)
        {
            return true;
        }

        var normalizedTag = tag.NormalizedValue;
        var query = dbContext.Contacts.AsNoTracking()
            .Where(c => c.Phone == phone && c.Tag.NormalizedValue == normalizedTag);

        if (excluding is { } excludedId)
        {
            query = query.Where(c => c.Id != excludedId);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }
}
