using MediatR;
using Microsoft.Extensions.Logging;
using PhoneBook.Application.Abstractions.Events;
using PhoneBook.Domain.Contacts.Events;

namespace PhoneBook.Application.Contacts.EventHandlers;

internal sealed partial class ContactTagChangedAuditHandler(ILogger<ContactTagChangedAuditHandler> logger)
    : INotificationHandler<DomainEventNotification<ContactTagChangedDomainEvent>>
{
    public Task Handle(DomainEventNotification<ContactTagChangedDomainEvent> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        LogTagChanged(logger, e.ContactId.Value, e.OldTag, e.NewTag);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Contact {ContactId} moved from tag {OldTag} to {NewTag}")]
    private static partial void LogTagChanged(ILogger logger, Guid contactId, string oldTag, string newTag);
}
