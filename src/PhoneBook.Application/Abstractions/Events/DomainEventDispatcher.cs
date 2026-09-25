using MediatR;
using Microsoft.Extensions.Logging;
using PhoneBook.SharedKernel.Domain;

namespace PhoneBook.Application.Abstractions.Events;

internal sealed class DomainEventDispatcher(IPublisher publisher, ILogger<DomainEventDispatcher> logger)
    : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in events)
        {
            var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
            var notification = (INotification)Activator.CreateInstance(notificationType, domainEvent)!;

            // The data is already committed: a failing handler must never turn a successful command into a 500.
            // Constitution Principle II explicitly allows this catch.
#pragma warning disable CA1031 // Catch general exception types — intentional isolation of post-commit handlers.
            try
            {
                await publisher.Publish(notification, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Domain event handler failed for {EventType} {EventId}",
                    domainEvent.GetType().Name,
                    domainEvent.EventId);
            }
#pragma warning restore CA1031
        }
    }
}
