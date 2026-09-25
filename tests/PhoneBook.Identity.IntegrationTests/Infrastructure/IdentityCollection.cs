namespace PhoneBook.Identity.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class IdentityCollection : ICollectionFixture<IdentityFactory>
{
    public const string Name = "Identity";
}
