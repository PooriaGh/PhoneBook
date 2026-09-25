namespace PhoneBook.SharedKernel.Domain;

public interface IDomainEvent
{
    Guid EventId { get; }

    DateTime OccurredOnUtc { get; }
}
