using PhoneBook.Domain.Contacts.Events;
using PhoneBook.SharedKernel.Domain;
using PhoneBook.SharedKernel.Results;
using PhoneBook.SharedKernel.Time;

namespace PhoneBook.Domain.Contacts;

/// <summary>A phone book entry. Aggregate root and consistency boundary.</summary>
public sealed class Contact : AggregateRoot<ContactId>
{
    // Required by EF Core for materialization.
    private Contact()
    {
        Name = null!;
        Phone = null!;
        Tag = null!;
    }

    private Contact(ContactId id, PersonName name, PhoneNumber phone, Tag tag, DateTime createdAtUtc)
    {
        Id = id;
        Name = name;
        Phone = phone;
        Tag = tag;
        Version = 1;
        CreatedAtUtc = createdAtUtc;
    }

    public PersonName Name { get; private set; }

    public PhoneNumber Phone { get; private set; }

    public Tag Tag { get; private set; }

    /// <summary>Optimistic-concurrency version; starts at 1 and increases on every change.</summary>
    public int Version { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public static Result<Contact> Create(PersonName name, PhoneNumber phone, Tag tag, IDateTimeProvider clock)
    {
        var contact = new Contact(ContactId.New(), name, phone, tag, clock.UtcNow);
        contact.Raise(new ContactCreatedDomainEvent(contact.Id, phone.Value, tag.Value));
        return contact;
    }

    /// <summary>
    /// Replaces all editable fields (FR-006). Identical values are a no-op. A tag that only differs in case
    /// updates the display value but is not a tag change.
    /// </summary>
    public Result Update(PersonName name, PhoneNumber phone, Tag tag, IDateTimeProvider clock)
    {
        var unchanged = Name == name
                        && Phone == phone
                        && string.Equals(Tag.Value, tag.Value, StringComparison.Ordinal);
        if (unchanged)
        {
            return Result.Success();
        }

        var oldTag = Tag;
        Name = name;
        Phone = phone;
        Tag = tag;
        Version++;
        UpdatedAtUtc = clock.UtcNow;

        Raise(new ContactUpdatedDomainEvent(Id));
        if (!oldTag.Equals(tag))
        {
            Raise(new ContactTagChangedDomainEvent(Id, oldTag.Value, tag.Value));
        }

        return Result.Success();
    }

    /// <summary>Records the deletion; the repository removes the aggregate and the event is published after commit.</summary>
    public Result MarkAsDeleted()
    {
        Raise(new ContactDeletedDomainEvent(Id, Tag.Value));
        return Result.Success();
    }
}
