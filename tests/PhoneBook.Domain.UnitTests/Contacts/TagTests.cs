using PhoneBook.Domain.Contacts;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Domain.UnitTests.Contacts;

public sealed class TagTests
{
    [Fact]
    public void Create_ValueWithSurroundingSpaces_TrimsAndNormalizes()
    {
        var tag = Tag.Create(" Work ").Value;

        tag.Value.ShouldBe("Work");
        tag.NormalizedValue.ShouldBe("WORK");
    }

    [Fact]
    public void Equality_DifferentCaseAndWhitespace_AreEqual()
    {
        var a = Tag.Create("work").Value;
        var b = Tag.Create("WORK ").Value;

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Equality_PrefixTag_IsNotEqual() =>
        Tag.Create("همکار").Value.ShouldNotBe(Tag.Create("همکاران").Value);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_Blank_ReturnsRequired(string? raw) =>
        AssertSingleError(Tag.Create(raw), "Tag.Required");

    [Fact]
    public void Create_51Characters_ReturnsTooLong() =>
        AssertSingleError(Tag.Create(new string('t', 51)), "Tag.TooLong");

    [Fact]
    public void Create_50Characters_Succeeds() =>
        Tag.Create(new string('t', Tag.MaxLength)).IsSuccess.ShouldBeTrue();

    [Fact]
    public void Normalize_TrimsAndUppercases() =>
        Tag.Normalize("  work ").ShouldBe("WORK");

    private static void AssertSingleError(Result result, string code)
    {
        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Errors.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            e => e.Field.ShouldBe("tag"),
            e => e.Code.ShouldBe(code));
    }
}
