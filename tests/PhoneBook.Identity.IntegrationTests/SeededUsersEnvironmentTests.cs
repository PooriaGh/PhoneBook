using System.Net;
using PhoneBook.Identity.IntegrationTests.Infrastructure;

namespace PhoneBook.Identity.IntegrationTests;

/// <summary>
/// Feature 002, FR-027: seeded end users exist only in Development. Outside it, configured users are ignored and a
/// warning without user names is logged.
/// </summary>
public sealed class SeededUsersEnvironmentTests
{
    [Fact]
    public async Task Production_IgnoresConfiguredUsers()
    {
        await using var production = new ConfiguredIdentityFactory(
            new Dictionary<string, string>
            {
                ["Identity:Users:0:UserName"] = "prod-alice",
                ["Identity:Users:0:Password"] = "prod-alice-password",
                ["Identity:Users:0:DisplayName"] = "Prod Alice",
                ["Identity:Users:0:Scopes:0"] = "phonebook.read",
            },
            environment: "Production");
        using var client = new PkceClient(production, "https://localhost");

        var response = await client.LoginAsync("/", "prod-alice", "prod-alice-password");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("Invalid username or password.");

        var logs = string.Join('\n', production.Logs.Entries);
        logs.ShouldContain("Configured users ignored outside Development");
        logs.ShouldContain("HTTP \"POST\" \"/account/login\"", Case.Sensitive, "request logs must reach this host's own sinks");
        logs.ShouldNotContain("prod-alice");
    }

    [Fact]
    public async Task Production_WarnsAtStartup_WithoutAnySignIn()
    {
        await using var production = new ConfiguredIdentityFactory(
            new Dictionary<string, string>
            {
                ["Identity:Users:0:UserName"] = "prod-bob",
                ["Identity:Users:0:Password"] = "prod-bob-password",
            },
            environment: "Production");

        _ = production.Services; // start the host; no request is sent

        var logs = string.Join('\n', production.Logs.Entries);
        logs.ShouldContain("Configured users ignored outside Development");
        logs.ShouldNotContain("prod-bob");
    }
}
