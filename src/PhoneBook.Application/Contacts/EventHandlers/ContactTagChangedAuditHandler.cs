using MediatR;
using Microsoft.Extensions.Logging;
using PhoneBook.Application.Abstractions.Events;
using PhoneBook.Domain.Contacts.Events;

namespace PhoneBook.Application.Contacts.EventHandlers;

/// <remarks>
/// Logs the contact id only: tag values are personal data (feature 002, FR-016). No counter, because a tag change
/// is also counted as an update.
/// </remarks>
internal sealed partial class ContactTagChangedAuditHandler(ILogger<ContactTagChangedAuditHandler> logger)
    : INotificationHandler<DomainEventNotification<ContactTagChangedDomainEvent>>
{
    public Task Handle(DomainEventNotification<ContactTagChangedDomainEvent> notification, CancellationToken cancellationToken)
    {
        LogTagChanged(logger, notification.DomainEvent.ContactId.Value);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Contact {ContactId} moved to another tag")]
    private static partial void LogTagChanged(ILogger logger, Guid contactId);
}
