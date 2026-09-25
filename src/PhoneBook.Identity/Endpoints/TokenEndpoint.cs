using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using PhoneBook.Identity.RateLimiting;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace PhoneBook.Identity.Endpoints;

/// <summary>
/// Token endpoint passthrough for the client-credentials grant. OpenIddict has already authenticated the
/// client and validated grant type and scopes before this runs; it only builds the access-token principal.
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
        HttpContext httpContext, IOpenIddictApplicationManager applications, IOptions<IdentitySettings> settings)
    {
        var request = httpContext.GetOpenIddictServerRequest();
        if (request is null || !request.IsClientCredentialsGrantType())
        {
            return Reject(Errors.UnsupportedGrantType, "Only the client_credentials grant is supported.");
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

    private static IResult Reject(string error, string description) => Results.Forbid(
        new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        }),
        [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
}
