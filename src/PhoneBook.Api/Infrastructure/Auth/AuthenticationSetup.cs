using System.Security.Claims;
using Microsoft.Extensions.Options;
using OpenIddict.Validation;
using OpenIddict.Validation.AspNetCore;

namespace PhoneBook.Api.Infrastructure.Auth;

internal static class AuthenticationSetup
{
    // OpenIddict stores granted scopes as private "oi_scp" claims; plain JWT handlers use a space-separated "scope".
    private const string OpenIddictScopeClaim = "oi_scp";
    private const string ScopeClaim = "scope";

    public static IServiceCollection AddPhoneBookAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOpenIddict()
            .AddValidation(options =>
            {
                options.UseSystemNetHttp();
                options.UseAspNetCore();
                options.AddEventHandler(ProblemDetailsChallengeHandler.Descriptor);
            });

        // Issuer and audience come from AuthOptions at runtime, so hosts and test factories can override Auth:*.
        services.AddOptions<OpenIddictValidationOptions>()
            .Configure<IOptions<AuthOptions>>((validation, auth) =>
            {
                validation.Issuer = new Uri(auth.Value.Authority, UriKind.Absolute);
                validation.Audiences.Add(auth.Value.Audience);
            });

        services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.ContactsRead, policy => policy.RequireAssertion(ctx => HasScope(ctx.User, Scopes.Read)))
            .AddPolicy(Policies.ContactsWrite, policy => policy.RequireAssertion(ctx => HasScope(ctx.User, Scopes.Write)));

        return services;
    }

    private static bool HasScope(ClaimsPrincipal user, string scope) =>
        user.FindAll(OpenIddictScopeClaim).Any(c => c.Value == scope)
        || user.FindAll(ScopeClaim)
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal);
}
