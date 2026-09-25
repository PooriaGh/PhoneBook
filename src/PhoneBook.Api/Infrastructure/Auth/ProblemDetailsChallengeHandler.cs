using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Validation.OpenIddictValidationEvents;

namespace PhoneBook.Api.Infrastructure.Auth;

/// <summary>
/// Makes real OpenIddict challenges (missing/invalid token) produce ProblemDetails, as constitution Principle III
/// requires (finding U6). Runs after the status code and <c>WWW-Authenticate</c> header are attached, writes the
/// problem body and marks the request handled so OpenIddict writes nothing else. No throw, no catch.
/// </summary>
internal sealed class ProblemDetailsChallengeHandler(IProblemDetailsService problemDetailsService, IHostEnvironment environment)
    : IOpenIddictValidationHandler<ProcessChallengeContext>
{
    public static OpenIddictValidationHandlerDescriptor Descriptor { get; } =
        OpenIddictValidationHandlerDescriptor.CreateBuilder<ProcessChallengeContext>()
            .UseScopedHandler<ProblemDetailsChallengeHandler>()
            .SetOrder(OpenIddictValidationAspNetCoreHandlers.AttachWwwAuthenticateHeader<ProcessChallengeContext>.Descriptor.Order + 1)
            .SetType(OpenIddictValidationHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(ProcessChallengeContext context)
    {
        var httpContext = context.Transaction.GetHttpRequest()?.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        var status = httpContext.Response.StatusCode is >= 400 ? httpContext.Response.StatusCode : StatusCodes.Status401Unauthorized;
        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = status == StatusCodes.Status401Unauthorized ? "Unauthorized" : "Bad request",
            Type = status == StatusCodes.Status401Unauthorized
                ? "https://tools.ietf.org/html/rfc9110#section-15.5.2"
                : "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Detail = environment.IsDevelopment() ? context.ErrorDescription : null,
        };

        // errorCode (Auth.Unauthorized for 401) and traceId are added by CustomizeProblemDetails.
        var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        }).ConfigureAwait(false);

        if (written)
        {
            context.HandleRequest();
        }
    }
}
