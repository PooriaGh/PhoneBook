using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;

namespace PhoneBook.Identity.Endpoints;

/// <summary>
/// The minimal sign-in page (feature 002, US4): English, left to right, labelled fields, antiforgery token. Every
/// dynamic value is HTML-encoded. Shared by the account endpoints and the rate-limit rejection page.
/// </summary>
internal static class LoginPage
{
    public const string InvalidCredentials = "Invalid username or password.";

    public static string Render(HttpContext context, string? returnUrl, string? message)
    {
        var tokens = context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
        var encoder = HtmlEncoder.Default;
        var error = message is null ? string.Empty : $"<p role=\"alert\" class=\"error\">{encoder.Encode(message)}</p>";

        return $$"""
            <!DOCTYPE html>
            <html lang="en" dir="ltr">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Sign in - PhoneBook</title>
              <style>
                body { font-family: system-ui, sans-serif; max-width: 22rem; margin: 4rem auto; padding: 0 1rem; }
                label { display: block; margin-top: 1rem; }
                input { width: 100%; padding: .5rem; box-sizing: border-box; }
                button { margin-top: 1.5rem; padding: .5rem 1rem; }
                .error { color: #b00020; }
              </style>
            </head>
            <body>
              <h1>Sign in</h1>
              {{error}}
              <form method="post" action="/account/login">
                <label for="username">Username</label>
                <input id="username" name="username" autocomplete="username" required>
                <label for="password">Password</label>
                <input id="password" name="password" type="password" autocomplete="current-password" required>
                <input type="hidden" name="ReturnUrl" value="{{encoder.Encode(returnUrl ?? "/")}}">
                <input type="hidden" name="{{encoder.Encode(tokens.FormFieldName)}}" value="{{encoder.Encode(tokens.RequestToken ?? string.Empty)}}">
                <button type="submit">Sign in</button>
              </form>
            </body>
            </html>
            """;
    }

    /// <remarks>Antiforgery marks the response <c>no-cache, no-store</c> itself, so no caching header is set here.</remarks>
    public static IResult Result(HttpContext context, string? returnUrl, string? message, int statusCode = StatusCodes.Status200OK) =>
        Results.Content(Render(context, returnUrl, message), "text/html; charset=utf-8", statusCode: statusCode);
}
