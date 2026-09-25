using MediatR;
using Microsoft.Extensions.Logging;
using PhoneBook.Application.Abstractions.Events;
using PhoneBook.Application.Abstractions.Telemetry;
using PhoneBook.Domain.Contacts.Events;

namespace PhoneBook.Application.Contacts.EventHandlers;

/// <remarks>Logs the contact id only: tag values are personal data (feature 002, FR-016).</remarks>
internal sealed partial class ContactCreatedAuditHandler(ILogger<ContactCreatedAuditHandler> logger, PhoneBookMetrics metrics)
    : INotificationHandler<DomainEventNotification<ContactCreatedDomainEvent>>
{
    public Task Handle(DomainEventNotification<ContactCreatedDomainEvent> notification, CancellationToken cancellationToken)
    {
        metrics.ContactsCreated.Add(1);
        LogCreated(logger, notification.DomainEvent.ContactId.Value);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Contact {ContactId} created")]
    private static partial void LogCreated(ILogger logger, Guid contactId);
}
