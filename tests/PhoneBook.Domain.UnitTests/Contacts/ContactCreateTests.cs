using PhoneBook.Domain.Contacts;
using PhoneBook.Domain.Contacts.Events;

namespace PhoneBook.Domain.UnitTests.Contacts;

public sealed class ContactCreateTests
{
    [Fact]
    public void Create_ValidParts_SetsIdentityVersionTimestampAndRaisesCreatedEvent()
    {
        var clock = new FixedClock(new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc));
        var name = PersonName.Create("علی", "رضایی").Value;
        var phone = PhoneNumber.Create("+98 912 123 4567").Value;
        var tag = Tag.Create("همکار").Value;

        var result = Contact.Create(name, phone, tag, clock);

        result.IsSuccess.ShouldBeTrue();
        var contact = result.Value;
        contact.Id.Value.ShouldNotBe(Guid.Empty);
        contact.Version.ShouldBe(1);
        contact.CreatedAtUtc.ShouldBe(clock.UtcNow);
        contact.UpdatedAtUtc.ShouldBeNull();
        contact.Name.ShouldBe(name);
        contact.Phone.ShouldBe(phone);
        contact.Tag.ShouldBe(tag);

        var created = contact.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ContactCreatedDomainEvent>();
        created.ContactId.ShouldBe(contact.Id);
        created.PhoneNumber.ShouldBe("+989121234567");
        created.Tag.ShouldBe("همکار");
    }

    [Fact]
    public void Create_TwoContacts_GetDistinctIds()
    {
        var clock = new FixedClock(DateTime.UtcNow);
        var name = PersonName.Create("a", "b").Value;
        var phone = PhoneNumber.Create("1234").Value;
        var tag = Tag.Create("t").Value;

        Contact.Create(name, phone, tag, clock).Value.Id
            .ShouldNotBe(Contact.Create(name, phone, tag, clock).Value.Id);
    }
}
