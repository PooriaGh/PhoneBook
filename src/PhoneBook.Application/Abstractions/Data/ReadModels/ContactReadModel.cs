namespace PhoneBook.Application.Abstractions.Data.ReadModels;

/// <summary>Flat projection of the <c>contacts</c> table for queries.</summary>
public sealed class ContactReadModel
{
    public Guid Id { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public string Tag { get; init; } = string.Empty;

    public string NormalizedTag { get; init; } = string.Empty;

    public int Version { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }
}
