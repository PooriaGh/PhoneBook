using MediatR;
using Microsoft.Extensions.Logging;
using PhoneBook.Application.Abstractions.Events;
using PhoneBook.Domain.Contacts.Events;

namespace PhoneBook.Application.Contacts.EventHandlers;

internal sealed partial class ContactDeletedAuditHandler(ILogger<ContactDeletedAuditHandler> logger)
    : INotificationHandler<DomainEventNotification<ContactDeletedDomainEvent>>
{
    public Task Handle(DomainEventNotification<ContactDeletedDomainEvent> notification, CancellationToken cancellationToken)
    {
        LogDeleted(logger, notification.DomainEvent.ContactId.Value, notification.DomainEvent.Tag);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Contact {ContactId} with tag {Tag} deleted")]
    private static partial void LogDeleted(ILogger logger, Guid contactId, string tag);
}
