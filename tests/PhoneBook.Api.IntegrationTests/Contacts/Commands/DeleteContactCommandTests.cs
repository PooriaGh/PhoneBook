using Microsoft.EntityFrameworkCore;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using PhoneBook.Application.Contacts.Delete;
using PhoneBook.Domain.Contacts;

namespace PhoneBook.Api.IntegrationTests.Contacts.Commands;

[Collection(IntegrationTestCollection.Name)]
public sealed class DeleteContactCommandTests(PhoneBookApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Delete_ExistingContact_RemovesRow()
    {
        var seeded = await SeedContactAsync("Ali", "Rezaei", "09121234567", "همکار");

        var result = await Sender.Send(new DeleteContactCommand(seeded.Id.Value, null), Ct);

        result.IsSuccess.ShouldBeTrue();
        (await WithReadDbAsync(db => db.Contacts.AnyAsync(c => c.Id == seeded.Id.Value, Ct))).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_Twice_SecondReturnsNotFound()
    {
        var seeded = await SeedContactAsync("Ali", "Rezaei", "09121234567", "همکار");
        await Sender.Send(new DeleteContactCommand(seeded.Id.Value, null), Ct);

        var result = await Sender.Send(new DeleteContactCommand(seeded.Id.Value, null), Ct);

        result.Error.Code.ShouldBe("Contact.NotFound");
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        var result = await Sender.Send(new DeleteContactCommand(Guid.NewGuid(), null), Ct);

        result.Error.Code.ShouldBe("Contact.NotFound");
    }

    [Fact]
    public async Task Delete_StaleExpectedVersion_ReturnsVersionMismatchAndKeepsRow()
    {
        var seeded = await SeedContactAsync("Ali", "Rezaei", "09121234567", "همکار");

        var result = await Sender.Send(new DeleteContactCommand(seeded.Id.Value, 5), Ct);

        result.Error.ShouldBe(ContactErrors.VersionMismatch);
        (await WithReadDbAsync(db => db.Contacts.AnyAsync(c => c.Id == seeded.Id.Value, Ct))).ShouldBeTrue();
    }
}
