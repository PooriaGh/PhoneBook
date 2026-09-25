using NSubstitute;
using PhoneBook.Domain.Contacts;
using PhoneBook.Domain.Contacts.Services;

namespace PhoneBook.Domain.UnitTests.Contacts;

public sealed class ContactDuplicateCheckerTests
{
    private readonly IContactUniquenessReader _reader = Substitute.For<IContactUniquenessReader>();
    private readonly PhoneNumber _phone = PhoneNumber.Create("09121234567").Value;
    private readonly Tag _tag = Tag.Create("همکار").Value;

    [Fact]
    public async Task EnsureNotDuplicate_ReaderFindsMatch_ReturnsDuplicate()
    {
        _reader.ExistsAsync(_phone, _tag, null, Arg.Any<CancellationToken>()).Returns(true);
        var sut = new ContactDuplicateChecker(_reader);

        var result = await sut.EnsureNotDuplicateAsync(_phone, _tag, null, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ContactErrors.Duplicate);
    }

    [Fact]
    public async Task EnsureNotDuplicate_NoMatch_ReturnsSuccess()
    {
        _reader.ExistsAsync(_phone, _tag, null, Arg.Any<CancellationToken>()).Returns(false);
        var sut = new ContactDuplicateChecker(_reader);

        var result = await sut.EnsureNotDuplicateAsync(_phone, _tag, null, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureNotDuplicate_WithExcludedId_PassesItToReader()
    {
        var excluded = ContactId.New();
        var sut = new ContactDuplicateChecker(_reader);

        await sut.EnsureNotDuplicateAsync(_phone, _tag, excluded, CancellationToken.None);

        await _reader.Received(1).ExistsAsync(_phone, _tag, excluded, Arg.Any<CancellationToken>());
    }
}
