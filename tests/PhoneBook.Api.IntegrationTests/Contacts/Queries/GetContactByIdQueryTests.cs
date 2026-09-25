using PhoneBook.Api.IntegrationTests.Infrastructure;
using PhoneBook.Application.Contacts.GetById;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Api.IntegrationTests.Contacts.Queries;

[Collection(IntegrationTestCollection.Name)]
public sealed class GetContactByIdQueryTests(PhoneBookApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task GetById_SeededContact_ReturnsItWithVersion()
    {
        var seeded = await SeedContactAsync("علی", "رضایی", "09121234567", "همکار");

        var result = await Sender.Send(new GetContactByIdQuery(seeded.Id.Value), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldSatisfyAllConditions(
            c => c.Id.ShouldBe(seeded.Id.Value),
            c => c.FirstName.ShouldBe("علی"),
            c => c.LastName.ShouldBe("رضایی"),
            c => c.PhoneNumber.ShouldBe("09121234567"),
            c => c.Tag.ShouldBe("همکار"),
            c => c.Version.ShouldBe(1));
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        var result = await Sender.Send(new GetContactByIdQuery(Guid.NewGuid()), Ct);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Contact.NotFound");
    }
}
