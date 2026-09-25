using PhoneBook.SharedKernel.Domain;

namespace PhoneBook.Application.Abstractions.Events;

public interface IDomainEventDispatcher
{
    /// <summary>Publishes events in-process after commit. Never throws.</summary>
    Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken);
}
