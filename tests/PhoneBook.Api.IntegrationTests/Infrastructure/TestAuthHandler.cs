using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PhoneBook.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Replaces OpenIddict validation in API tests. The <c>X-Test-Scopes</c> header controls the caller:
/// missing → both scopes; <c>none</c> → unauthenticated (401); otherwise → that value as the <c>scope</c> claim.
/// The real token round-trip is covered by the Identity end-to-end test.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string ScopesHeader = "X-Test-Scopes";
    public const string NoScopes = "none";
    public const string AllScopes = "phonebook.read phonebook.write";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var scopes = Request.Headers.TryGetValue(ScopesHeader, out var value) ? value.ToString() : AllScopes;
        if (scopes == NoScopes)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            [new Claim("sub", "test-client"), new Claim("scope", scopes)], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
