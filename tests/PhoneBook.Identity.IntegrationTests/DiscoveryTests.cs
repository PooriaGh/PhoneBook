using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PhoneBook.Identity.IntegrationTests.Infrastructure;

namespace PhoneBook.Identity.IntegrationTests;

[Collection(IdentityCollection.Name)]
public sealed class DiscoveryTests(IdentityFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Discovery_ReturnsClientCredentialsTokenEndpointAndKeys()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/.well-known/openid-configuration", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var discovery = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        discovery.GetProperty("issuer").GetString().ShouldBe(IdentityFactory.Issuer);
        discovery.GetProperty("token_endpoint").GetString().ShouldEndWith("/connect/token");
        discovery.GetProperty("grant_types_supported").EnumerateArray().Select(e => e.GetString())
            .ShouldContain("client_credentials");

        var jwksUri = new Uri(discovery.GetProperty("jwks_uri").GetString()!);
        var jwks = await client.GetFromJsonAsync<JsonElement>(jwksUri.PathAndQuery, Ct);
        jwks.GetProperty("keys").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task TokenPreflight_FromApiOrigin_IsAllowed()
    {
        using var client = factory.CreateClient();

        var response = await SendPreflightAsync(client, IdentityFactory.ApiOrigin);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins).ShouldBeTrue();
        origins.ShouldHaveSingleItem().ShouldBe(IdentityFactory.ApiOrigin);
    }

    [Fact]
    public async Task TokenPreflight_FromUnlistedOrigin_IsNotAllowed()
    {
        using var client = factory.CreateClient();

        var response = await SendPreflightAsync(client, "https://evil.example");

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    private static async Task<HttpResponseMessage> SendPreflightAsync(HttpClient client, string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri("/connect/token", UriKind.Relative));
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        return await client.SendAsync(request, Ct);
    }
}
