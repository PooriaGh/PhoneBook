using PhoneBook.Api.IntegrationTests.Infrastructure;
using PhoneBook.Application.Contacts.GetByTag;

namespace PhoneBook.Api.IntegrationTests.Contacts.Queries;

// Feature 002 (FR-006): the query now returns a page; expectations and order are unchanged (SC-008).

[Collection(IntegrationTestCollection.Name)]
public sealed class GetContactsByTagQueryTests(PhoneBookApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task GetByTag_MixedTags_ReturnsOnlyExactMatches()
    {
        await SeedContactAsync("علی", "رضایی", "09120000001", "همکار");
        await SeedContactAsync("مریم", "احمدی", "09120000002", "همکار");
        await SeedContactAsync("سارا", "کریمی", "09120000003", "همکار");
        await SeedContactAsync("حسن", "نوری", "09120000004", "خانواده");
        await SeedContactAsync("زهرا", "نوری", "09120000005", "خانواده");

        var result = await Sender.Send(new GetContactsByTagQuery("همکار"), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(3);
        result.Value.TotalCount.ShouldBe(3);
        result.Value.Items.ShouldAllBe(c => c.Tag == "همکار");
    }

    [Fact]
    public async Task GetByTag_DifferentCaseAndSurroundingSpaces_Matches()
    {
        await SeedContactAsync("Ali", "Rezaei", "09121234567", "Work");

        var result = await Sender.Send(new GetContactsByTagQuery(" work "), Ct);

        result.Value.Items.ShouldHaveSingleItem().Tag.ShouldBe("Work");
    }

    [Fact]
    public async Task GetByTag_PrefixOfAnotherTag_DoesNotMatch()
    {
        await SeedContactAsync("Ali", "Rezaei", "09121234567", "همکاران");

        var result = await Sender.Send(new GetContactsByTagQuery("همکار"), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByTag_UnknownTag_ReturnsEmptySuccess()
    {
        await SeedContactAsync("Ali", "Rezaei", "09121234567", "همکار");

        var result = await Sender.Send(new GetContactsByTagQuery("دوست"), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByTag_PersianAndLatinNames_OrderedOrdinallyByLastThenFirstName()
    {
        await SeedContactAsync("علی", "رضایی", "09120000001", "tag");
        await SeedContactAsync("مریم", "احمدی", "09120000002", "tag");
        await SeedContactAsync("حسن", "احمدی", "09120000003", "tag");
        await SeedContactAsync("John", "Smith", "09120000004", "tag");
        await SeedContactAsync("Anna", "smith", "09120000005", "tag");

        var result = await Sender.Send(new GetContactsByTagQuery("tag"), Ct);

        var expected = result.Value.Items
            .OrderBy(c => c.LastName, StringComparer.Ordinal)
            .ThenBy(c => c.FirstName, StringComparer.Ordinal)
            .Select(c => $"{c.LastName}/{c.FirstName}")
            .ToList();
        result.Value.Items.Select(c => $"{c.LastName}/{c.FirstName}").ShouldBe(expected);
        expected.ShouldBe(["Smith/John", "smith/Anna", "احمدی/حسن", "احمدی/مریم", "رضایی/علی"]);
    }
}
