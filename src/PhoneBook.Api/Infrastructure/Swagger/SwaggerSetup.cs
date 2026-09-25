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
                Description = "Phone book resource API. Obtain a token from PhoneBook.Identity (client credentials).",
            });

            options.OperationFilter<AuthorizeOperationFilter>();
        });

        // The token URL comes from AuthOptions at runtime (Auth:PublicAuthority, finding U5).
        services.AddOptions<SwaggerGenOptions>()
            .Configure<IOptions<AuthOptions>>((options, auth) =>
                options.AddSecurityDefinition(SchemeName, new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Description = "OAuth 2.0 client credentials issued by PhoneBook.Identity.",
                    Flows = new OpenApiOAuthFlows
                    {
                        ClientCredentials = new OpenApiOAuthFlow
                        {
                            TokenUrl = new Uri(
                                $"{auth.Value.EffectivePublicAuthority.TrimEnd('/')}/connect/token", UriKind.Absolute),
                            Scopes = new Dictionary<string, string>
                            {
                                [Scopes.Read] = "Read contacts",
                                [Scopes.Write] = "Create, update and delete contacts",
                            },
                        },
                    },
                }));

        return services;
    }

    public static WebApplication UsePhoneBookSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "PhoneBook API v1");
            options.OAuthClientId("phonebook-swagger");
            options.OAuthScopes(Scopes.Read, Scopes.Write);
        });
        return app;
    }

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
