using PhoneBook.SharedKernel.Domain;

namespace PhoneBook.Domain.Contacts.Events;

public sealed record ContactTagChangedDomainEvent(ContactId ContactId, string OldTag, string NewTag) : DomainEvent;
