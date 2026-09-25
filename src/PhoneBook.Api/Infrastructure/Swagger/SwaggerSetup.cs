using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using PhoneBook.Api.Infrastructure.Auth;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PhoneBook.Api.Infrastructure.Swagger;

internal static class SwaggerSetup
{
    private const string SchemeName = "oauth2";

    public static IServiceCollection AddPhoneBookSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "PhoneBook API",
                Version = "v1",
                Description = "Phone book resource API. Obtain a token from PhoneBook.Identity: client credentials for "
                    + "applications, or authorization code + PKCE for people (feature 002).",
            });

            options.OperationFilter<AuthorizeOperationFilter>();
        });

        // The authority URLs come from AuthOptions at runtime (Auth:PublicAuthority, finding U5).
        services.AddOptions<SwaggerGenOptions>()
            .Configure<IOptions<AuthOptions>>((options, auth) =>
            {
                var authority = auth.Value.EffectivePublicAuthority.TrimEnd('/');
                options.AddSecurityDefinition(SchemeName, new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Description = "OAuth 2.0 issued by PhoneBook.Identity: client credentials (applications) or "
                        + "authorization code with PKCE (people, feature 002).",
                    Flows = new OpenApiOAuthFlows
                    {
                        ClientCredentials = new OpenApiOAuthFlow
                        {
                            TokenUrl = new Uri($"{authority}/connect/token", UriKind.Absolute),
                            Scopes = ApiScopes(),
                        },
                        AuthorizationCode = new OpenApiOAuthFlow
                        {
                            AuthorizationUrl = new Uri($"{authority}/connect/authorize", UriKind.Absolute),
                            TokenUrl = new Uri($"{authority}/connect/token", UriKind.Absolute),
                            Scopes = ApiScopes(),
                        },
                    },
                });
            });

        return services;
    }

    public static WebApplication UsePhoneBookSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "PhoneBook API v1");
            // Swagger UI pre-fills one client id for every flow, so it is the secret-less public client used for
            // end-user sign-in (feature 002). For client credentials, type "phonebook-swagger" and its secret.
            options.OAuthClientId("phonebook-swagger-ui");
            options.OAuthUsePkce();
            options.OAuthScopes(Scopes.Read, Scopes.Write);
        });
        return app;
    }

    private static Dictionary<string, string> ApiScopes() => new()
    {
        [Scopes.Read] = "Read contacts",
        [Scopes.Write] = "Create, update and delete contacts",
    };

    /// <summary>Adds the OAuth2 requirement only to operations that carry authorization metadata.</summary>
    private sealed class AuthorizeOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
            if (!metadata.OfType<IAuthorizeData>().Any() || metadata.OfType<IAllowAnonymous>().Any())
            {
                return;
            }

            var policies = metadata.OfType<IAuthorizeData>().Select(a => a.Policy).ToList();
            List<string> scopes = [];
            if (policies.Contains(Policies.ContactsRead))
            {
                scopes.Add(Scopes.Read);
            }

            if (policies.Contains(Policies.ContactsWrite))
            {
                scopes.Add(Scopes.Write);
            }

            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = scopes,
            });
        }
    }
}
