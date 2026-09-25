using PhoneBook.SharedKernel.Domain;

namespace PhoneBook.Domain.Contacts.Events;

public sealed record ContactCreatedDomainEvent(ContactId ContactId, string PhoneNumber, string Tag) : DomainEvent;
