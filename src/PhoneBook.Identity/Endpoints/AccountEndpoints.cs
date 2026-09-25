using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using PhoneBook.Identity.RateLimiting;
using PhoneBook.Identity.Users;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace PhoneBook.Identity.Endpoints;

/// <summary>
/// The sign-in form (feature 002, contracts/identity-signin.md). There is no registration and no sign-out endpoint:
/// accounts come only from configuration, and sessions simply expire (AC6; sign-out is out of scope).
/// </summary>
internal static partial class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("account/login", ([FromQuery(Name = "ReturnUrl")] string? returnUrl, HttpContext context) =>
                LoginPage.Result(context, SafeReturnUrl(returnUrl), message: null))
            .ExcludeFromDescription();

        app.MapPost("account/login", SignInAsync)
            .RequireRateLimiting(TokenRateLimitOptions.TokenPolicy)
            .ExcludeFromDescription();

        return app;
    }

    /// <summary>Antiforgery is validated automatically for form-bound minimal APIs (FR-025).</summary>
    private static async Task<IResult> SignInAsync(
        [FromForm(Name = "username")] string? userName,
        [FromForm(Name = "password")] string? password,
        [FromForm(Name = "ReturnUrl")] string? returnUrl,
        HttpContext context,
        InMemoryUserStore users,
        ILogger<InMemoryUserStore> logger)
    {
        var safeReturnUrl = SafeReturnUrl(returnUrl);
        var user = users.ValidateCredentials(userName, password);
        if (user is null)
        {
            LogSignInFailed(logger);
            return LoginPage.Result(context, safeReturnUrl, LoginPage.InvalidCredentials);
        }

        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme, Claims.Name, Claims.Role);
        identity.AddClaim(new Claim(Claims.Subject, user.Id.ToString()));
        identity.AddClaim(new Claim(Claims.Name, user.DisplayName));
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity)).ConfigureAwait(false);

        LogSignedIn(logger);
        return Results.LocalRedirect(safeReturnUrl);
    }

    /// <summary>
    /// Only local paths are allowed: a leading <c>/</c> that is not <c>//</c> or <c>/\</c>. Anything else becomes
    /// <c>/</c>, so the form can never be used as an open redirect (FR-024).
    /// </summary>
    internal static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl)
        && returnUrl[0] == '/'
        && (returnUrl.Length == 1 || (returnUrl[1] != '/' && returnUrl[1] != '\\'))
            ? returnUrl
            : "/";

    // Outcomes only; never the user name or password (FR-016).
    [LoggerMessage(Level = LogLevel.Information, Message = "Sign-in failed")]
    private static partial void LogSignInFailed(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sign-in succeeded")]
    private static partial void LogSignedIn(ILogger logger);
}
