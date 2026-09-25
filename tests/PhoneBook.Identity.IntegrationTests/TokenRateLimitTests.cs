using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PhoneBook.Identity.IntegrationTests.Infrastructure;

namespace PhoneBook.Identity.IntegrationTests;

/// <summary>
/// Feature 002, US2: the Identity host's <c>token</c> policy (FR-008, FR-009, FR-010). Each test creates its own host,
/// because all in-process requests share one address partition (research R-07).
/// </summary>
public sealed class TokenRateLimitTests : IAsyncLifetime
{
    private readonly RateLimitedIdentityFactory _factory = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Token_OverAllowance_Returns429WithOAuthBody()
    {
        using var client = _factory.CreateClient();
        for (var i = 0; i < RateLimitedIdentityFactory.Limit; i++)
        {
            (await RequestValidTokenAsync(client)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var response = await RequestValidTokenAsync(client);

        await ShouldBeTokenRateLimitedAsync(response);
    }

    [Fact]
    public async Task Token_WrongSecrets_AreCountedToo()
    {
        using var client = _factory.CreateClient();
        for (var i = 0; i < RateLimitedIdentityFactory.Limit; i++)
        {
            var wrong = await TokenClient.RequestAsync(client, TokenClient.SwaggerClientId, "wrong-secret", "phonebook.read");
            wrong.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
            wrong.IsSuccessStatusCode.ShouldBeFalse();
        }

        var response = await TokenClient.RequestAsync(client, TokenClient.SwaggerClientId, "wrong-secret", "phonebook.read");

        await ShouldBeTokenRateLimitedAsync(response);
    }

    [Fact]
    public async Task HealthDiscoveryAndKeys_AreNeverLimited()
    {
        using var client = _factory.CreateClient();
        var discovery = await client.GetFromJsonAsync<JsonElement>(
            new Uri("/.well-known/openid-configuration", UriKind.Relative), Ct);
        var jwks = new Uri(discovery.GetProperty("jwks_uri").GetString()!, UriKind.Absolute).PathAndQuery;

        for (var i = 0; i < 20; i++)
        {
            (await client.GetAsync(new Uri("/health/ready", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await client.GetAsync(new Uri("/.well-known/openid-configuration", UriKind.Relative), Ct)).StatusCode
                .ShouldBe(HttpStatusCode.OK);
            (await client.GetAsync(new Uri(jwks, UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    private static Task<HttpResponseMessage> RequestValidTokenAsync(HttpClient client) =>
        TokenClient.RequestAsync(client, TokenClient.SwaggerClientId, TokenClient.SwaggerClientSecret, "phonebook.read");

    private static async Task ShouldBeTokenRateLimitedAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        response.Headers.TryGetValues("Retry-After", out var values).ShouldBeTrue();
        int.Parse(values!.Single(), System.Globalization.CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(1);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("error").GetString().ShouldBe("temporarily_unavailable");
        body.GetProperty("error_description").GetString().ShouldBe("Too many requests. Retry later.");
    }
}
