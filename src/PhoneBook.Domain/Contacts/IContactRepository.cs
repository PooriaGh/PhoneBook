namespace PhoneBook.Domain.Contacts;

/// <summary>Write-side access to <see cref="Contact"/> aggregates. Changes are committed by the Unit of Work.</summary>
public interface IContactRepository
{
    Task<Contact?> GetByIdAsync(ContactId id, CancellationToken cancellationToken);

    void Add(Contact contact);

    void Remove(Contact contact);
}
