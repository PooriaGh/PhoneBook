using PhoneBook.SharedKernel.Domain;

namespace PhoneBook.Domain.Contacts.Events;

public sealed record ContactDeletedDomainEvent(ContactId ContactId, string Tag) : DomainEvent;
