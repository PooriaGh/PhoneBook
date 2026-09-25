using PhoneBook.Application.Abstractions.Messaging;

namespace PhoneBook.Application.Contacts.Update;

/// <param name="ExpectedVersion">Version from <c>If-Match</c>; <c>null</c> means no precondition.</param>
public sealed record UpdateContactCommand(
    Guid Id, string FirstName, string LastName, string PhoneNumber, string Tag, int? ExpectedVersion)
    : ICommand<ContactResponse>;
