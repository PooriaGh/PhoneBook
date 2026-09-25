namespace PhoneBook.Api.IntegrationTests.Infrastructure;

/// <summary>PostgreSQL-backed tests share one factory and run sequentially (Respawn resets between tests).</summary>
[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<PhoneBookApiFactory>
{
    public const string Name = "Postgres";
}
