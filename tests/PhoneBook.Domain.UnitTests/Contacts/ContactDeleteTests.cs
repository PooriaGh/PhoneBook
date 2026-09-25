using PhoneBook.Domain.Contacts;
using PhoneBook.Domain.Contacts.Events;

namespace PhoneBook.Domain.UnitTests.Contacts;

public sealed class ContactDeleteTests
{
    [Fact]
    public void MarkAsDeleted_RaisesSingleDeletedEventWithIdAndTag()
    {
        var contact = Contact.Create(
            PersonName.Create("Ali", "Rezaei").Value,
            PhoneNumber.Create("09121234567").Value,
            Tag.Create("همکار").Value,
            new FixedClock(DateTime.UtcNow)).Value;
        contact.ClearDomainEvents();

        var result = contact.MarkAsDeleted();

        result.IsSuccess.ShouldBeTrue();
        var deleted = contact.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ContactDeletedDomainEvent>();
        deleted.ContactId.ShouldBe(contact.Id);
        deleted.Tag.ShouldBe("همکار");
    }
}
