using MediatR;
using Microsoft.Extensions.Logging;
using PhoneBook.Application.Abstractions.Events;
using PhoneBook.Application.Abstractions.Telemetry;
using PhoneBook.Domain.Contacts.Events;

namespace PhoneBook.Application.Contacts.EventHandlers;

internal sealed partial class ContactUpdatedAuditHandler(ILogger<ContactUpdatedAuditHandler> logger, PhoneBookMetrics metrics)
    : INotificationHandler<DomainEventNotification<ContactUpdatedDomainEvent>>
{
    public Task Handle(DomainEventNotification<ContactUpdatedDomainEvent> notification, CancellationToken cancellationToken)
    {
        metrics.ContactsUpdated.Add(1);
        LogUpdated(logger, notification.DomainEvent.ContactId.Value);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Contact {ContactId} updated")]
    private static partial void LogUpdated(ILogger logger, Guid contactId);
}
