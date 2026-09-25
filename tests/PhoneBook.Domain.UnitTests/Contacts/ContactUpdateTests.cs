using PhoneBook.Domain.Contacts;
using PhoneBook.Domain.Contacts.Events;

namespace PhoneBook.Domain.UnitTests.Contacts;

public sealed class ContactUpdateTests
{
    private readonly FixedClock _clock = new(new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Update_ChangedName_IncrementsVersionSetsUpdatedAtAndRaisesUpdated()
    {
        var contact = NewContact("Work");
        var id = contact.Id;
        _clock.UtcNow = _clock.UtcNow.AddMinutes(5);

        var result = contact.Update(Name("Reza", "Alavi"), Phone("09121234567"), Tag("Work"), _clock);

        result.IsSuccess.ShouldBeTrue();
        contact.Id.ShouldBe(id);
        contact.Version.ShouldBe(2);
        contact.UpdatedAtUtc.ShouldBe(_clock.UtcNow);
        contact.Name.FirstName.ShouldBe("Reza");
        contact.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ContactUpdatedDomainEvent>().ContactId.ShouldBe(id);
    }

    [Fact]
    public void Update_ChangedTag_AlsoRaisesTagChanged()
    {
        var contact = NewContact("همکار");

        contact.Update(Name("Ali", "Rezaei"), Phone("09121234567"), Tag("دوست"), _clock);

        contact.DomainEvents.Count.ShouldBe(2);
        contact.DomainEvents.OfType<ContactUpdatedDomainEvent>().ShouldHaveSingleItem();
        var changed = contact.DomainEvents.OfType<ContactTagChangedDomainEvent>().ShouldHaveSingleItem();
        changed.OldTag.ShouldBe("همکار");
        changed.NewTag.ShouldBe("دوست");
    }

    [Fact]
    public void Update_TagDiffersOnlyInCase_UpdatesDisplayValueWithoutTagChangedEvent()
    {
        var contact = NewContact("Work");

        contact.Update(Name("Ali", "Rezaei"), Phone("09121234567"), Tag("work"), _clock);

        contact.Tag.Value.ShouldBe("work");
        contact.Version.ShouldBe(2);
        contact.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ContactUpdatedDomainEvent>();
    }

    [Fact]
    public void Update_IdenticalValues_IsNoOp()
    {
        var contact = NewContact("Work");

        var result = contact.Update(Name("Ali", "Rezaei"), Phone("0912-123-4567"), Tag("Work"), _clock);

        result.IsSuccess.ShouldBeTrue();
        contact.Version.ShouldBe(1);
        contact.UpdatedAtUtc.ShouldBeNull();
        contact.DomainEvents.ShouldBeEmpty();
    }

    private Contact NewContact(string tag)
    {
        var contact = Contact.Create(Name("Ali", "Rezaei"), Phone("09121234567"), Tag(tag), _clock).Value;
        contact.ClearDomainEvents();
        return contact;
    }

    private static PersonName Name(string first, string last) => PersonName.Create(first, last).Value;

    private static PhoneNumber Phone(string raw) => PhoneNumber.Create(raw).Value;

    private static Tag Tag(string raw) => global::PhoneBook.Domain.Contacts.Tag.Create(raw).Value;
}
