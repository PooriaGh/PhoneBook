using PhoneBook.SharedKernel.Domain;

namespace PhoneBook.Domain.Contacts.Events;

public sealed record ContactUpdatedDomainEvent(ContactId ContactId) : DomainEvent;
