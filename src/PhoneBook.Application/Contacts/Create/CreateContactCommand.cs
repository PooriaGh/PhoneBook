using PhoneBook.Application.Abstractions.Messaging;

namespace PhoneBook.Application.Contacts.Create;

public sealed record CreateContactCommand(string FirstName, string LastName, string PhoneNumber, string Tag)
    : ICommand<ContactResponse>;
