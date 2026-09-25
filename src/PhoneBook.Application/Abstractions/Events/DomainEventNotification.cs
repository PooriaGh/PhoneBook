using MediatR;
using PhoneBook.SharedKernel.Domain;

namespace PhoneBook.Application.Abstractions.Events;

/// <summary>Adapts a MediatR-agnostic domain event to a MediatR notification, keeping the Domain pure.</summary>
public sealed record DomainEventNotification<TEvent>(TEvent DomainEvent) : INotification
    where TEvent : IDomainEvent;
