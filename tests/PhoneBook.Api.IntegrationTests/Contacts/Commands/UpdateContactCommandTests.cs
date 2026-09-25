using Microsoft.EntityFrameworkCore;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using PhoneBook.Application.Contacts.GetByTag;
using PhoneBook.Application.Contacts.Update;
using PhoneBook.Domain.Contacts;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Api.IntegrationTests.Contacts.Commands;

[Collection(IntegrationTestCollection.Name)]
public sealed class UpdateContactCommandTests(PhoneBookApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Update_ValidCommand_UpdatesRowAndVersion()
    {
        var seeded = await SeedContactAsync("Ali", "Rezaei", "09121234567", "همکار");

        var result = await Sender.Send(
            new UpdateContactCommand(seeded.Id.Value, "Reza", "Alavi", "09129999999", "همکار", null), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Version.ShouldBe(2);
        var stored = await WithReadDbAsync(db => db.Contacts.SingleAsync(c => c.Id == seeded.Id.Value, Ct));
        stored.FirstName.ShouldBe("Reza");
        stored.PhoneNumber.ShouldBe("09129999999");
        stored.Version.ShouldBe(2);
        stored.UpdatedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        var result = await Sender.Send(
            new UpdateContactCommand(Guid.NewGuid(), "Reza", "Alavi", "09129999999", "همکار", null), Ct);

        result.Error.Code.ShouldBe("Contact.NotFound");
    }

    [Fact]
    public async Task Update_InvalidValues_ReturnsValidationErrorAndLeavesRowUnchanged()
    {
        var seeded = await SeedContactAsync("Ali", "Rezaei", "09121234567", "همکار");

        var result = await Sender.Send(
            new UpdateContactCommand(seeded.Id.Value, "Ali", " ", "09121234567", "همکار", null), Ct);

        result.Error.ShouldBeOfType<ValidationError>().Errors.ShouldHaveSingleItem().Field.ShouldBe("lastName");
        var stored = await WithReadDbAsync(db => db.Contacts.SingleAsync(c => c.Id == seeded.Id.Value, Ct));
        stored.LastName.ShouldBe("Rezaei");
        stored.Version.ShouldBe(1);
    }

    [Fact]
    public async Task Update_ToAnotherContactsPhoneAndTag_ReturnsDuplicate()
    {
        await SeedContactAsync("Ali", "Rezaei", "09121111111", "همکار");
        var other = await SeedContactAsync("Reza", "Alavi", "09122222222", "دوست");

        var result = await Sender.Send(
            new UpdateContactCommand(other.Id.Value, "Reza", "Alavi", "09121111111", "همکار", null), Ct);

        result.Error.ShouldBe(ContactErrors.Duplicate);
    }

    [Fact]
    public async Task Update_KeepingOwnPhoneAndTag_Succeeds()
    {
        var seeded = await SeedContactAsync("Ali", "Rezaei", "09121234567", "همکار");

        var result = await Sender.Send(
            new UpdateContactCommand(seeded.Id.Value, "Ali", "Rezaei-Updated", "09121234567", "همکار", null), Ct);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_StaleExpectedVersion_ReturnsVersionMismatch()
    {
        var seeded = await SeedContactAsync("Ali", "Rezaei", "09121234567", "همکار");

        var result = await Sender.Send(
            new UpdateContactCommand(seeded.Id.Value, "Reza", "Alavi", "09121234567", "همکار", 99), Ct);

        result.Error.ShouldBe(ContactErrors.VersionMismatch);
    }

    [Fact]
    public async Task Update_TagChanged_ContactMovesBetweenTagSearches()
    {
        var seeded = await SeedContactAsync("Ali", "Rezaei", "09121234567", "همکار");

        await Sender.Send(new UpdateContactCommand(seeded.Id.Value, "Ali", "Rezaei", "09121234567", "دوست", null), Ct);

        (await Sender.Send(new GetContactsByTagQuery("همکار"), Ct)).Value.ShouldBeEmpty();
        (await Sender.Send(new GetContactsByTagQuery("دوست"), Ct)).Value.ShouldHaveSingleItem().Id.ShouldBe(seeded.Id.Value);
    }
}
