using MediatR;
using Microsoft.Extensions.Logging;
using PhoneBook.Application.Abstractions.Events;
using PhoneBook.Application.Abstractions.Telemetry;
using PhoneBook.Domain.Contacts.Events;

namespace PhoneBook.Application.Contacts.EventHandlers;

/// <remarks>Logs the contact id only: tag values are personal data (feature 002, FR-016).</remarks>
internal sealed partial class ContactDeletedAuditHandler(ILogger<ContactDeletedAuditHandler> logger, PhoneBookMetrics metrics)
    : INotificationHandler<DomainEventNotification<ContactDeletedDomainEvent>>
{
    public Task Handle(DomainEventNotification<ContactDeletedDomainEvent> notification, CancellationToken cancellationToken)
    {
        metrics.ContactsDeleted.Add(1);
        LogDeleted(logger, notification.DomainEvent.ContactId.Value);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Contact {ContactId} deleted")]
    private static partial void LogDeleted(ILogger logger, Guid contactId);
}
