using PhoneBook.Domain.Contacts;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Domain.UnitTests.Contacts;

public sealed class PhoneNumberTests
{
    [Theory]
    [InlineData("+98 912-123-4567", "+989121234567")]
    [InlineData("۰۹۱۲۱۲۳۴۵۶۷", "09121234567")]
    [InlineData("٠٩١٢", "0912")]
    [InlineData("  0912 123 4567  ", "09121234567")]
    [InlineData("1234", "1234")]
    [InlineData("123456789012345", "123456789012345")]
    [InlineData("+123456789012345", "+123456789012345")]
    public void Create_ValidInput_ReturnsNormalizedValue(string raw, string expected)
    {
        var result = PhoneNumber.Create(raw);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12+34")]
    [InlineData("0912_123")]
    [InlineData("++98912")]
    public void Create_DisallowedCharacters_ReturnsInvalidCharacters(string raw) =>
        AssertSingleError(PhoneNumber.Create(raw), "PhoneNumber.InvalidCharacters");

    [Theory]
    [InlineData("123")]
    [InlineData("1234567890123456")]
    [InlineData("+")]
    [InlineData("- -")]
    public void Create_WrongDigitCount_ReturnsInvalidLength(string raw) =>
        AssertSingleError(PhoneNumber.Create(raw), "PhoneNumber.InvalidLength");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Blank_ReturnsRequired(string? raw) =>
        AssertSingleError(PhoneNumber.Create(raw), "PhoneNumber.Required");

    [Fact]
    public void Equality_SameNormalizedValue_AreEqual() =>
        PhoneNumber.Create("0912-123-4567").Value.ShouldBe(PhoneNumber.Create("۰۹۱۲۱۲۳۴۵۶۷").Value);

    private static void AssertSingleError(Result result, string code)
    {
        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Errors.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            e => e.Field.ShouldBe("phoneNumber"),
            e => e.Code.ShouldBe(code));
    }
}
