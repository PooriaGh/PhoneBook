using PhoneBook.Application.Abstractions.Messaging;

namespace PhoneBook.Application.Contacts.GetById;

public sealed record GetContactByIdQuery(Guid Id) : IQuery<ContactResponse>;
