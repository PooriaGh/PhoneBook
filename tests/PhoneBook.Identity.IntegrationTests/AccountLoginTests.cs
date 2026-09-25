using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using PhoneBook.Identity.IntegrationTests.Infrastructure;

namespace PhoneBook.Identity.IntegrationTests;

/// <summary>
/// Feature 002, US4: the sign-in page (contracts/identity-signin.md; FR-021, FR-024 to FR-026; English page per the
/// spec's Clarifications).
/// </summary>
[Collection(IdentityCollection.Name)]
public sealed partial class AccountLoginTests(IdentityFactory factory)
{
    private const string GenericError = "Invalid username or password.";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_ReturnsEnglishFormWithLabelsAndAntiforgeryToken()
    {
        using var pkce = new PkceClient(factory);

        var response = await pkce.Http.GetAsync(new Uri("/account/login?ReturnUrl=%2F", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        response.Headers.CacheControl?.NoStore.ShouldBeTrue();
        var html = await response.Content.ReadAsStringAsync(Ct);
        html.ShouldContain("lang=\"en\"");
        html.ShouldContain("dir=\"ltr\"");
        html.ShouldContain("<label for=\"username\"");
        html.ShouldContain("<label for=\"password\"");
        html.ShouldContain("name=\"ReturnUrl\"");
        PkceClient.AntiforgeryToken(html).ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Post_WrongPasswordAndUnknownUser_ShowSameGenericErrorWithoutCookie()
    {
        using var wrongPassword = new PkceClient(factory);
        using var unknownUser = new PkceClient(factory);

        var a = await wrongPassword.LoginAsync("/", "alice", "not-the-password");
        var b = await unknownUser.LoginAsync("/", "nobody", "whatever");

        foreach (var response in new[] { a, b })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Headers.TryGetValues("Set-Cookie", out var cookies);
            (cookies ?? []).ShouldNotContain(c => c.StartsWith(".AspNetCore.Cookies", StringComparison.Ordinal));
        }

        var bodyA = WithoutToken(await a.Content.ReadAsStringAsync(Ct));
        var bodyB = WithoutToken(await b.Content.ReadAsStringAsync(Ct));
        bodyA.ShouldContain(GenericError);
        bodyA.ShouldBe(bodyB);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task Post_UnknownUserTiming_IsComparableToWrongPassword()
    {
        using var client = new PkceClient(factory);
        await client.LoginAsync("/", "alice", "warm-up");

        var wrong = new List<double>();
        var unknown = new List<double>();
        for (var i = 0; i < 10; i++)
        {
            wrong.Add(await TimeLoginAsync(client, "alice", "wrong-password"));
            unknown.Add(await TimeLoginAsync(client, "nobody", "wrong-password"));
        }

        var ratio = Median(unknown) / Median(wrong);
        ratio.ShouldBeInRange(0.5, 2.0, "an unknown account must not answer measurably faster or slower (FR-021)");
    }

    [Fact]
    public async Task Post_WithoutAntiforgeryToken_Returns400()
    {
        using var pkce = new PkceClient(factory);

        var response = await pkce.Http.PostAsync(
            new Uri("/account/login", UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = "alice", ["password"] = "alice-dev-password" }),
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("https://evil.example/x")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    public async Task Post_ExternalReturnUrl_RedirectsToRoot(string returnUrl)
    {
        using var pkce = new PkceClient(factory);

        var response = await pkce.LoginAsync(returnUrl, "alice", "alice-dev-password");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe("/");
    }

    [Theory]
    [InlineData("GET", "/account/register")]
    [InlineData("POST", "/account/register")]
    [InlineData("GET", "/account/logout")]
    public async Task RegistrationAndSignOut_DoNotExist(string method, string path)
    {
        using var client = factory.CreateClient();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative)), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_ValidLogin_OverHttps_SetsHardenedSessionCookie()
    {
        using var pkce = new PkceClient(factory, "https://localhost");

        var response = await pkce.LoginAsync("/", "alice", "alice-dev-password");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".AspNetCore.Cookies", StringComparison.Ordinal));
        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldContain("samesite=lax", Case.Insensitive);
        cookie.ShouldContain("secure", Case.Insensitive);

        // A browser-session cookie: the 15-minute lifetime is enforced server-side in the authentication ticket
        // (covered by the session-expiry test). If a browser ever receives an expiry, it must not exceed 15 minutes.
        var expires = ExpiresPattern().Match(cookie);
        if (expires.Success)
        {
            (DateTimeOffset.Parse(expires.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) - DateTimeOffset.UtcNow)
                .TotalMinutes.ShouldBeLessThanOrEqualTo(16);
        }
    }

    private static async Task<double> TimeLoginAsync(PkceClient client, string user, string password)
    {
        var stopwatch = Stopwatch.StartNew();
        (await client.LoginAsync("/", user, password)).StatusCode.ShouldBe(HttpStatusCode.OK);
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private static double Median(List<double> values)
    {
        var sorted = values.Order().ToList();
        return (sorted[(sorted.Count - 1) / 2] + sorted[sorted.Count / 2]) / 2;
    }

    private static string WithoutToken(string html) => TokenValuePattern().Replace(html, "value=\"\"");

    [GeneratedRegex("expires=([^;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ExpiresPattern();

    [GeneratedRegex("(?<=name=\"__RequestVerificationToken\"[^>]*)value=\"[^\"]*\"")]
    private static partial Regex TokenValuePattern();
}
