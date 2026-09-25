using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using PhoneBook.Identity.IntegrationTests.Infrastructure;

namespace PhoneBook.Identity.IntegrationTests;

/// <summary>
/// Feature 002, US4: authorization code + PKCE, following contracts/identity-signin.md (FR-018 to FR-020, FR-024,
/// FR-026). Seeded DEV-ONLY users: alice (read and write) and bob (read).
/// </summary>
[Collection(IdentityCollection.Name)]
public sealed class AuthorizationCodeFlowTests(IdentityFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Discovery_AdvertisesCodeFlowWithS256Only()
    {
        using var client = factory.CreateClient();

        var discovery = await client.GetFromJsonAsync<JsonElement>(new Uri("/.well-known/openid-configuration", UriKind.Relative), Ct);

        discovery.GetProperty("authorization_endpoint").GetString().ShouldEndWith("/connect/authorize");
        Strings(discovery, "grant_types_supported").ShouldContain("authorization_code");
        Strings(discovery, "grant_types_supported").ShouldContain("client_credentials");
        Strings(discovery, "response_types_supported").ShouldContain("code");
        Strings(discovery, "code_challenge_methods_supported").ShouldBe(["S256"]);
    }

    [Fact]
    public async Task Authorize_Anonymous_RedirectsToLogin()
    {
        using var pkce = new PkceClient(factory);

        var response = await pkce.Http.GetAsync(PkceClient.BuildAuthorizeUri(PkceClient.AllScopes, PkceClient.CreatePkcePair().Challenge), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldContain("/account/login?ReturnUrl=");
    }

    [Fact]
    public async Task Alice_GetsTokenWithBothScopesAndStableSubject()
    {
        using var pkce = new PkceClient(factory);
        var (verifier, challenge) = PkceClient.CreatePkcePair();
        var code = await pkce.GetCodeAsync("alice", "alice-dev-password", PkceClient.AllScopes, challenge);

        var response = await pkce.RedeemAsync(code, verifier);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("token_type").GetString().ShouldBe("Bearer");
        body.GetProperty("expires_in").GetInt32().ShouldBeInRange(3590, 3600); // seconds remaining of the 1-hour lifetime
        var jwt = PkceClient.ReadJwtPayload(body.GetProperty("access_token").GetString()!);
        Guid.TryParse(jwt.GetProperty("sub").GetString(), out _).ShouldBeTrue();
        jwt.GetProperty("name").GetString().ShouldNotBeNullOrWhiteSpace();
        jwt.GetProperty("aud").ToString().ShouldContain("phonebook-api");
        jwt.GetProperty("scope").GetString()!.Split(' ').Order(StringComparer.Ordinal).ShouldBe(["phonebook.read", "phonebook.write"]);

        using var again = new PkceClient(factory);
        var second = PkceClient.ReadJwtPayload(await again.GetAccessTokenAsync("alice", "alice-dev-password"));
        second.GetProperty("sub").GetString().ShouldBe(jwt.GetProperty("sub").GetString());
    }

    [Fact]
    public async Task Bob_AskingForBothScopes_IsDownScopedToRead()
    {
        using var pkce = new PkceClient(factory);

        var jwt = PkceClient.ReadJwtPayload(await pkce.GetAccessTokenAsync("bob", "bob-dev-password", PkceClient.AllScopes));

        jwt.GetProperty("scope").GetString().ShouldBe("phonebook.read");
    }

    [Theory]
    [InlineData("phonebook.write")]
    [InlineData(null)]
    public async Task Authorize_NothingGrantable_RedirectsWithAccessDenied(string? scope)
    {
        using var pkce = new PkceClient(factory);

        var redirect = await pkce.AuthorizeWithLoginAsync(
            PkceClient.BuildAuthorizeUri(scope, PkceClient.CreatePkcePair().Challenge), "bob", "bob-dev-password");

        redirect.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = redirect.Headers.Location!;
        location.GetLeftPart(UriPartial.Path).ShouldBe(IdentityFactory.SwaggerRedirectUri);
        QueryHelpers.ParseQuery(location.Query)["error"].ToString().ShouldBe("access_denied");
    }

    /// <remarks>
    /// Observed OpenIddict behaviour (recorded in the contract): PKCE parameters are validated before the redirect URI,
    /// so the error is rendered by the Identity host as <c>400 invalid_request</c> instead of being redirected. Either
    /// way no code is issued (FR-018).
    /// </remarks>
    [Theory]
    [InlineData(null, "S256")]
    [InlineData("challenge-placeholder", "plain")]
    public async Task Authorize_MissingOrPlainPkce_IsRejectedWithInvalidRequest(string? challenge, string method)
    {
        using var pkce = new PkceClient(factory);
        var value = challenge is null ? null : PkceClient.CreatePkcePair().Verifier; // plain: challenge == verifier

        var response = await pkce.Http.GetAsync(PkceClient.BuildAuthorizeUri(PkceClient.AllScopes, value, method), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Headers.Location.ShouldBeNull();
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("invalid_request");
    }

    [Fact]
    public async Task Token_WrongOrMissingVerifier_IsRejected()
    {
        using var pkce = new PkceClient(factory);
        var (_, challenge) = PkceClient.CreatePkcePair();

        var wrong = await pkce.RedeemAsync(
            await pkce.GetCodeAsync("alice", "alice-dev-password", PkceClient.AllScopes, challenge), PkceClient.CreatePkcePair().Verifier);
        var missing = await pkce.RedeemAsync(
            await pkce.GetCodeAsync("alice", "alice-dev-password", PkceClient.AllScopes, challenge), verifier: null);

        await ShouldBeOAuthErrorAsync(wrong, "invalid_grant");
        await ShouldBeOAuthErrorAsync(missing, "invalid_request"); // observed OpenIddict code for a missing verifier (CHK015)
    }

    [Fact]
    public async Task Token_CodeRedeemedTwice_IsRejectedAndRevokesFirstToken()
    {
        using var pkce = new PkceClient(factory);
        var (verifier, challenge) = PkceClient.CreatePkcePair();
        var code = await pkce.GetCodeAsync("alice", "alice-dev-password", PkceClient.AllScopes, challenge);

        var first = await pkce.RedeemAsync(code, verifier);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var accessToken = (await first.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("access_token").GetString()!;

        await ShouldBeOAuthErrorAsync(await pkce.RedeemAsync(code, verifier), "invalid_grant");

        var tokenId = PkceClient.ReadJwtPayload(accessToken).GetProperty("oi_tkn_id").GetString()!;
        await using var scope = factory.Services.CreateAsyncScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var entry = await tokens.FindByIdAsync(tokenId, Ct);
        entry.ShouldNotBeNull();
        (await tokens.GetStatusAsync(entry, Ct)).ShouldBe(OpenIddictConstants.Statuses.Revoked);
    }

    [Fact]
    public async Task Token_ExpiredCode_IsInvalidGrant()
    {
        await using var shortLived = new ConfiguredIdentityFactory(new Dictionary<string, string>
        {
            ["Identity:AuthorizationCodeLifetime"] = "00:00:02",
        });
        using var pkce = new PkceClient(shortLived);
        var (verifier, challenge) = PkceClient.CreatePkcePair();
        var code = await pkce.GetCodeAsync("alice", "alice-dev-password", PkceClient.AllScopes, challenge);

        await Task.Delay(TimeSpan.FromSeconds(3), Ct);

        await ShouldBeOAuthErrorAsync(await pkce.RedeemAsync(code, verifier), "invalid_grant");
    }

    [Fact]
    public async Task Authorize_SessionExpiredMidSignIn_AsksToSignInAgain()
    {
        await using var shortSession = new ConfiguredIdentityFactory(new Dictionary<string, string>
        {
            ["Identity:SessionLifetime"] = "00:00:02",
        });
        using var pkce = new PkceClient(shortSession);
        var authorize = PkceClient.BuildAuthorizeUri(PkceClient.AllScopes, PkceClient.CreatePkcePair().Challenge);
        var challenge = await pkce.Http.GetAsync(authorize, Ct);
        var returnUrl = QueryHelpers.ParseQuery(new Uri(new Uri("http://localhost"), challenge.Headers.Location!).Query)["ReturnUrl"].ToString();
        (await pkce.LoginAsync(returnUrl, "alice", "alice-dev-password")).StatusCode.ShouldBe(HttpStatusCode.Redirect);

        await Task.Delay(TimeSpan.FromSeconds(3), Ct);

        var response = await pkce.Http.GetAsync(authorize, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldContain("/account/login");
    }

    [Theory]
    [InlineData(PkceClient.ClientId, "https://localhost:7001/swagger/oauth2-redirect.html/")]
    [InlineData(PkceClient.ClientId, "https://evil.example/callback")]
    [InlineData("unknown-client", IdentityFactory.SwaggerRedirectUri)]
    public async Task Authorize_UnknownClientOrUnregisteredRedirect_NeverRedirects(string clientId, string redirectUri)
    {
        using var pkce = new PkceClient(factory);

        var response = await pkce.Http.GetAsync(
            PkceClient.BuildAuthorizeUri(PkceClient.AllScopes, PkceClient.CreatePkcePair().Challenge, clientId: clientId, redirectUri: redirectUri),
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Headers.Location.ShouldBeNull();
    }

    private static string[] Strings(JsonElement element, string property) =>
        element.GetProperty(property).EnumerateArray().Select(e => e.GetString()!).ToArray();

    private static async Task ShouldBeOAuthErrorAsync(HttpResponseMessage response, string error)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        JsonDocument.Parse(body).RootElement.GetProperty("error").GetString().ShouldBe(error, body);
    }
}
