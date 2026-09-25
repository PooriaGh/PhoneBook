namespace PhoneBook.Api.IntegrationTests.Infrastructure;

/// <summary>Common surface of the PostgreSQL and SQLite factories so the same tests can run on both providers.</summary>
public interface IPhoneBookApiFactory
{
    IServiceProvider Services { get; }

    Task ResetDatabaseAsync();

    HttpClient CreateClientWithScopes(string? scopes = null);
}
