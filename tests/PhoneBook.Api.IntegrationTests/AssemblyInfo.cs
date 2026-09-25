using PhoneBook.Api.IntegrationTests.Infrastructure;

// One PostgreSQL container for the whole test run; Respawn resets it between tests.
[assembly: AssemblyFixture(typeof(PostgresContainerFixture))]
