using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using PhoneBook.Identity.Users;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace PhoneBook.Identity.Endpoints;

/// <summary>
/// Authorization endpoint passthrough for the authorization-code flow (feature 002, contracts/identity-signin.md).
/// </summary>
/// <remarks>
/// OpenIddict has already rejected unknown clients, non-exact redirect URIs and missing or <c>plain</c> PKCE before
/// this runs (FR-018, FR-024). Consent is implicit for the first-party client, so there is no consent screen.
/// </remarks>
internal static class AuthorizeEndpoint
{
    public static IEndpointRouteBuilder MapAuthorizeEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapMethods("connect/authorize", [HttpMethods.Get, HttpMethods.Post], HandleAsync).ExcludeFromDescription();
        return app;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context, InMemoryUserStore users, IOptions<IdentitySettings> settings)
    {
        var request = context.GetOpenIddictServerRequest();
        if (request is null)
        {
            return Results.BadRequest();
        }

        var session = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        var user = session.Succeeded && Guid.TryParse(session.Principal?.FindFirstValue(Claims.Subject), out var id)
            ? users.FindById(id)
            : null;
        if (user is null)
        {
            // Not signed in (or the session expired, or the user no longer exists): sign in, then come back here.
            var query = context.Request.HasFormContentType ? QueryString.Create(context.Request.Form) : context.Request.QueryString;
            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = context.Request.PathBase + context.Request.Path + query },
                [CookieAuthenticationDefaults.AuthenticationScheme]);
        }

        // Down-scoping (FR-020): only the requested scopes the user holds. Nothing grantable means access denied.
        var granted = request.GetScopes().Intersect(user.Scopes, StringComparer.Ordinal).ToList();
        if (granted.Count == 0)
        {
            return Results.Forbid(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.AccessDenied,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The user holds none of the requested permissions.",
                }),
                [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, user.Id.ToString());
        identity.SetClaim(Claims.Name, user.DisplayName);
        identity.SetScopes(granted);
        identity.SetResources(settings.Value.Audience);
        identity.SetDestinations(static _ => [Destinations.AccessToken]);

        return Results.SignIn(new ClaimsPrincipal(identity), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
