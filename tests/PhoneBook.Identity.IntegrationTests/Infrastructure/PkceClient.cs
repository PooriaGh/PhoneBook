using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;

namespace PhoneBook.Identity.IntegrationTests.Infrastructure;

/// <summary>
/// Drives the authorization-code + PKCE flow without a browser (feature 002, US4): authorize → login form (with the
/// antiforgery token) → authorize → code → token. Redirects are not followed, so every hop can be asserted.
/// </summary>
public sealed partial class PkceClient : IDisposable
{
    public const string ClientId = "phonebook-swagger-ui";
    public const string AllScopes = "phonebook.read phonebook.write";

    private static readonly Uri AuthorizePath = new("/connect/authorize", UriKind.Relative);

    public PkceClient(IdentityFactory factory, string baseAddress = "http://localhost") =>
        Http = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri(baseAddress),
        });

    public HttpClient Http { get; }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static (string Verifier, string Challenge) CreatePkcePair()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32)); // 43 characters
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    public static Uri BuildAuthorizeUri(
        string? scope, string? challenge, string method = "S256", string clientId = ClientId,
        string redirectUri = IdentityFactory.SwaggerRedirectUri, string state = "state-123")
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
        };
        if (scope is not null)
        {
            query["scope"] = scope;
        }

        if (challenge is not null)
        {
            query["code_challenge"] = challenge;
            query["code_challenge_method"] = method;
        }

        return new Uri(QueryHelpers.AddQueryString(AuthorizePath.OriginalString, query), UriKind.Relative);
    }

    /// <summary>GETs the login form, then POSTs the credentials with its antiforgery token.</summary>
    public async Task<HttpResponseMessage> LoginAsync(string returnUrl, string userName, string password)
    {
        var form = await Http.GetAsync(new Uri($"/account/login?ReturnUrl={Uri.EscapeDataString(returnUrl)}", UriKind.Relative), Ct);
        form.StatusCode.ShouldBe(HttpStatusCode.OK);
        var token = AntiforgeryToken(await form.Content.ReadAsStringAsync(Ct));

        return await Http.PostAsync(new Uri("/account/login", UriKind.Relative), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = userName,
            ["password"] = password,
            ["ReturnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = token,
        }), Ct);
    }

    /// <summary>Runs authorize → login → authorize and returns the final redirect.</summary>
    public async Task<HttpResponseMessage> AuthorizeWithLoginAsync(Uri authorizeUri, string userName, string password)
    {
        var challenge = await Http.GetAsync(authorizeUri, Ct);
        challenge.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var loginLocation = challenge.Headers.Location!;
        if (!loginLocation.OriginalString.Contains("/account/login", StringComparison.Ordinal))
        {
            return challenge; // already signed in: authorize answered directly
        }

        var returnUrl = QueryHelpers.ParseQuery(ToAbsolute(loginLocation).Query)["ReturnUrl"].ToString();

        var login = await LoginAsync(returnUrl, userName, password);
        login.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        return await Http.GetAsync(login.Headers.Location!, Ct);
    }

    /// <summary>Signs in and returns the authorization code from the redirect to the client.</summary>
    public async Task<string> GetCodeAsync(string userName, string password, string scope, string challenge)
    {
        var redirect = await AuthorizeWithLoginAsync(BuildAuthorizeUri(scope, challenge), userName, password);
        redirect.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = redirect.Headers.Location!;
        location.GetLeftPart(UriPartial.Path).ShouldBe(IdentityFactory.SwaggerRedirectUri);
        var query = QueryHelpers.ParseQuery(location.Query);
        query["state"].ToString().ShouldBe("state-123");
        var code = query["code"].ToString();
        code.ShouldNotBeNullOrEmpty();
        return code;
    }

    public Task<HttpResponseMessage> RedeemAsync(string code, string? verifier, string redirectUri = IdentityFactory.SwaggerRedirectUri)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = ClientId,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
        };
        if (verifier is not null)
        {
            form["code_verifier"] = verifier;
        }

        return Http.PostAsync(new Uri("/connect/token", UriKind.Relative), new FormUrlEncodedContent(form), Ct);
    }

    /// <summary>Full flow: returns the access token for <paramref name="userName"/>.</summary>
    public async Task<string> GetAccessTokenAsync(string userName, string password, string scope = AllScopes)
    {
        var (verifier, challenge) = CreatePkcePair();
        var code = await GetCodeAsync(userName, password, scope, challenge);
        var response = await RedeemAsync(code, verifier);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("access_token").GetString()!;
    }

    public static JsonElement ReadJwtPayload(string jwt)
    {
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
        return JsonDocument.Parse(Convert.FromBase64String(payload)).RootElement.Clone();
    }

    public static string AntiforgeryToken(string html) =>
        AntiforgeryPattern().Match(html) is { Success: true } match
            ? WebUtility.HtmlDecode(match.Groups[1].Value)
            : throw new InvalidOperationException("No antiforgery token in the login page.");

    public void Dispose() => Http.Dispose();

    private static Uri ToAbsolute(Uri uri) => uri.IsAbsoluteUri ? uri : new Uri(new Uri("http://localhost"), uri);

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryPattern();
}
