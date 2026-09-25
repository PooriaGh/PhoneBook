namespace PhoneBook.Domain.Contacts.Services;

/// <summary>Domain-owned port used by <see cref="ContactDuplicateChecker"/>; implemented in Infrastructure.</summary>
public interface IContactUniquenessReader
{
    Task<bool> ExistsAsync(PhoneNumber phone, Tag tag, ContactId? excluding, CancellationToken cancellationToken);
}
