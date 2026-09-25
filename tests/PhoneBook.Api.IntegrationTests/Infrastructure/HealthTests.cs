using System.Net;

namespace PhoneBook.Api.IntegrationTests.Infrastructure;

[Collection(IntegrationTestCollection.Name)]
public sealed class HealthTests(PhoneBookApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Ready_WithPostgres_ReturnsHealthy()
    {
        var response = await Client.GetAsync(new Uri("/health/ready", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

[Collection(SqliteIntegrationTestCollection.Name)]
public sealed class SqliteHealthTests(SqliteApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Ready_WithInMemorySqlite_ReturnsHealthy()
    {
        var response = await Client.GetAsync(new Uri("/health/ready", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
