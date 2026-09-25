using Bogus;

namespace PhoneBook.Api.IntegrationTests.Infrastructure;

/// <summary>Persian-locale test data: names, valid Iranian mobile numbers and word tags.</summary>
public static class ContactFaker
{
    private static readonly Faker Faker = new("fa");

    public static string FirstName() => Faker.Name.FirstName();

    public static string LastName() => Faker.Name.LastName();

    public static string Phone() => Faker.Phone.PhoneNumber("+98912#######");

    public static string Tag() => Faker.Lorem.Word();

    public static ContactRequestData Request(string? tag = null, string? phone = null) =>
        new(FirstName(), LastName(), phone ?? Phone(), tag ?? Tag());
}

public sealed record ContactRequestData(string FirstName, string LastName, string PhoneNumber, string Tag);
