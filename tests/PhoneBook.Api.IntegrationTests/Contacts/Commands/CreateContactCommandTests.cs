using Microsoft.EntityFrameworkCore;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using PhoneBook.Application.Contacts.Create;
using PhoneBook.Domain.Contacts;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Api.IntegrationTests.Contacts.Commands;

[Collection(IntegrationTestCollection.Name)]
public sealed class CreateContactCommandTests(PhoneBookApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Create_ValidCommand_PersistsContactWithNormalizedPhone()
    {
        var result = await Sender.Send(new CreateContactCommand("علی", "رضایی", "+98 912 123 4567", "همکار"), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.PhoneNumber.ShouldBe("+989121234567");
        result.Value.Version.ShouldBe(1);

        var stored = await WithReadDbAsync(db => db.Contacts.SingleAsync(c => c.Id == result.Value.Id, Ct));
        stored.PhoneNumber.ShouldBe("+989121234567");
        stored.FirstName.ShouldBe("علی");
        stored.Tag.ShouldBe("همکار");
        stored.NormalizedTag.ShouldBe("همکار".ToUpperInvariant());
    }

    [Fact]
    public async Task Create_SamePhoneAndTagDifferingInCase_ReturnsDuplicate()
    {
        await Sender.Send(new CreateContactCommand("Ali", "Rezaei", "09121234567", "hamkar"), Ct);

        var result = await Sender.Send(new CreateContactCommand("Reza", "Alavi", "0912-123-4567", " HAMKAR "), Ct);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ContactErrors.Duplicate);
    }

    [Fact]
    public async Task Create_SamePhoneDifferentTag_Succeeds()
    {
        await Sender.Send(new CreateContactCommand("Ali", "Rezaei", "09121234567", "همکار"), Ct);

        var result = await Sender.Send(new CreateContactCommand("Ali", "Rezaei", "09121234567", "دوست"), Ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_BlankLastNameAndInvalidPhone_ReturnsBothFieldErrorsAndPersistsNothing()
    {
        var result = await Sender.Send(new CreateContactCommand("Ali", " ", "abc", "همکار"), Ct);

        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Errors.Select(e => e.Field).ShouldBe(["lastName", "phoneNumber"], ignoreOrder: true);
        error.Errors.Single(e => e.Field == "lastName").Code.ShouldBe("PersonName.LastName.Required");
        error.Errors.Single(e => e.Field == "phoneNumber").Code.ShouldBe("PhoneNumber.InvalidCharacters");

        (await WithReadDbAsync(db => db.Contacts.CountAsync(Ct))).ShouldBe(0);
    }
}
