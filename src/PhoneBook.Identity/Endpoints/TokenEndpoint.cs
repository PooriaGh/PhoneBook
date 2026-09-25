using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using PhoneBook.Identity.Users;
using PhoneBook.Identity.RateLimiting;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace PhoneBook.Identity.Endpoints;

/// <summary>
/// Token endpoint passthrough. OpenIddict has already authenticated the client and validated the grant before this
/// runs. For client credentials it builds the access-token principal; for the authorization code (feature 002) it
/// re-issues the principal stored in the code, after OpenIddict has verified PKCE and single use (FR-019).
/// </summary>
internal static class TokenEndpoint
{
    public static IEndpointRouteBuilder MapTokenEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("connect/token", HandleAsync)
            .RequireRateLimiting(TokenRateLimitOptions.TokenPolicy)
            .ExcludeFromDescription();
        return app;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext httpContext, IOpenIddictApplicationManager applications, IOptions<IdentitySettings> settings, InMemoryUserStore users)
    {
        var request = httpContext.GetOpenIddictServerRequest();
        if (request is not null && request.IsAuthorizationCodeGrantType())
        {
            return await RedeemAuthorizationCodeAsync(httpContext, users).ConfigureAwait(false);
        }

        if (request is null || !request.IsClientCredentialsGrantType())
        {
            return Reject(Errors.UnsupportedGrantType, "Only the client_credentials and authorization_code grants are supported.");
        }

        var application = await applications.FindByClientIdAsync(request.ClientId ?? string.Empty);
        if (application is null)
        {
            return Reject(Errors.InvalidClient, "The client application was not found.");
        }

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, await applications.GetClientIdAsync(application));
        identity.SetClaim(Claims.Name, await applications.GetDisplayNameAsync(application));
        identity.SetScopes(request.GetScopes());
        identity.SetResources(settings.Value.Audience);
        identity.SetDestinations(static _ => [Destinations.AccessToken]);

        return Results.SignIn(new ClaimsPrincipal(identity), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> RedeemAuthorizationCodeAsync(HttpContext httpContext, InMemoryUserStore users)
    {
        var result = await httpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme).ConfigureAwait(false);
        var principal = result.Principal;
        if (principal is null
            || !Guid.TryParse(principal.GetClaim(Claims.Subject), out var userId)
            || users.FindById(userId) is null)
        {
            return Reject(Errors.InvalidGrant, "The authorization code is no longer valid.");
        }

        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static IResult Reject(string error, string description) => Results.Forbid(
        new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        }),
        [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
}
