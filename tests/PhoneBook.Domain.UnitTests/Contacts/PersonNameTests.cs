using PhoneBook.Domain.Contacts;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Domain.UnitTests.Contacts;

public sealed class PersonNameTests
{
    [Fact]
    public void Create_PersianNamesWithZwnjAndSpaces_TrimsAndKeepsText()
    {
        var result = PersonName.Create("  محمد‌رضا ", " رضایی  ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.FirstName.ShouldBe("محمد‌رضا");
        result.Value.LastName.ShouldBe("رضایی");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankFirstName_ReturnsFirstNameRequired(string? firstName)
    {
        var result = PersonName.Create(firstName, "Rezaei");

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Errors.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            e => e.Field.ShouldBe("firstName"),
            e => e.Code.ShouldBe("PersonName.FirstName.Required"));
    }

    [Fact]
    public void Create_LastNameOf101Characters_ReturnsLastNameTooLong()
    {
        var result = PersonName.Create("Ali", new string('x', 101));

        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Errors.ShouldHaveSingleItem().Code.ShouldBe("PersonName.LastName.TooLong");
    }

    [Fact]
    public void Create_NamesOfExactly100Characters_Succeeds()
    {
        var result = PersonName.Create(new string('a', PersonName.MaxLength), new string('b', PersonName.MaxLength));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_BothPartsInvalid_ReturnsOneValidationErrorWithTwoFieldErrors()
    {
        var result = PersonName.Create(" ", new string('x', 101));

        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Errors.Count.ShouldBe(2);
        error.Errors.Select(e => e.Field).ShouldBe(["firstName", "lastName"], ignoreOrder: true);
    }
}
