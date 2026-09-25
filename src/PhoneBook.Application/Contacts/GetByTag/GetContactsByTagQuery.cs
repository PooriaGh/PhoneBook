using PhoneBook.Application.Abstractions.Messaging;

namespace PhoneBook.Application.Contacts.GetByTag;

public sealed record GetContactsByTagQuery(string Tag) : IQuery<IReadOnlyList<ContactResponse>>;
