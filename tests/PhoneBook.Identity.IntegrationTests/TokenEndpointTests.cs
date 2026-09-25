using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using PhoneBook.Identity.IntegrationTests.Infrastructure;

namespace PhoneBook.Identity.IntegrationTests;

[Collection(IdentityCollection.Name)]
public sealed class TokenEndpointTests(IdentityFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Token_SwaggerClientBothScopes_ReturnsBearerJwtForApiAudience()
    {
        using var client = factory.CreateClient();

        var response = await TokenClient.RequestAsync(
            client, TokenClient.SwaggerClientId, TokenClient.SwaggerClientSecret, "phonebook.read phonebook.write");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("token_type").GetString().ShouldBe("Bearer");
        body.GetProperty("expires_in").GetInt32().ShouldBeGreaterThan(0);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(body.GetProperty("access_token").GetString());
        jwt.Issuer.ShouldBe(IdentityFactory.Issuer);
        jwt.Audiences.ShouldContain("phonebook-api");
        jwt.Subject.ShouldBe(TokenClient.SwaggerClientId);
        var scopes = jwt.Claims.Where(c => c.Type == "scope")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToList();
        scopes.ShouldBe(["phonebook.read", "phonebook.write"], ignoreOrder: true);
    }

    [Fact]
    public async Task Token_WrongSecret_ReturnsInvalidClient() =>
        await AssertOAuthErrorAsync(
            await TokenClient.RequestAsync(factory.CreateClient(), TokenClient.SwaggerClientId, "wrong", "phonebook.read"),
            "invalid_client");

    [Fact]
    public async Task Token_ReadOnlyClientRequestingWrite_ReturnsInvalidRequest() =>
        // OpenIddict rejects a scope the client has no permission for with invalid_request (ID2051);
        // invalid_scope is reserved for scopes that do not exist (see the next test).
        await AssertOAuthErrorAsync(
            await TokenClient.RequestAsync(
                factory.CreateClient(), TokenClient.ReadOnlyClientId, TokenClient.ReadOnlyClientSecret, "phonebook.write"),
            "invalid_request");

    [Fact]
    public async Task Token_UnknownScope_ReturnsInvalidScope() =>
        await AssertOAuthErrorAsync(
            await TokenClient.RequestAsync(
                factory.CreateClient(), TokenClient.SwaggerClientId, TokenClient.SwaggerClientSecret, "phonebook.admin"),
            "invalid_scope");

    [Fact]
    public async Task Token_PasswordGrant_ReturnsUnsupportedGrantType() =>
        await AssertOAuthErrorAsync(
            await TokenClient.RequestAsync(
                factory.CreateClient(), TokenClient.SwaggerClientId, TokenClient.SwaggerClientSecret, null, grantType: "password"),
            "unsupported_grant_type");

    private static async Task AssertOAuthErrorAsync(HttpResponseMessage response, string error)
    {
        ((int)response.StatusCode).ShouldBeInRange(400, 401);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("error").GetString().ShouldBe(error);
    }
}
