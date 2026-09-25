using PhoneBook.Application.Abstractions.Messaging;

namespace PhoneBook.Application.Contacts.Delete;

/// <param name="ExpectedVersion">Version from <c>If-Match</c>; <c>null</c> means no precondition.</param>
public sealed record DeleteContactCommand(Guid Id, int? ExpectedVersion) : ICommand;
