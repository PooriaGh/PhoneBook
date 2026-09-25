using MediatR;
using Microsoft.Extensions.Logging;
using PhoneBook.Application.Abstractions.Events;
using PhoneBook.Domain.Contacts.Events;

namespace PhoneBook.Application.Contacts.EventHandlers;

internal sealed partial class ContactCreatedAuditHandler(ILogger<ContactCreatedAuditHandler> logger)
    : INotificationHandler<DomainEventNotification<ContactCreatedDomainEvent>>
{
    public Task Handle(DomainEventNotification<ContactCreatedDomainEvent> notification, CancellationToken cancellationToken)
    {
        LogCreated(logger, notification.DomainEvent.ContactId.Value, notification.DomainEvent.Tag);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Contact {ContactId} created with tag {Tag}")]
    private static partial void LogCreated(ILogger logger, Guid contactId, string tag);
}
