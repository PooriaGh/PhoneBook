namespace PhoneBook.Api.IntegrationTests.Infrastructure;

/// <summary>Tests against the default in-memory SQLite provider.</summary>
[CollectionDefinition(Name)]
public sealed class SqliteIntegrationTestCollection : ICollectionFixture<SqliteApiFactory>
{
    public const string Name = "Sqlite";
}
