using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace PhoneBook.Api.IntegrationTests.Infrastructure;

internal static class TestAuthExtensions
{
    public static IServiceCollection AddTestAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        return services;
    }

    public static HttpClient WithSub(this HttpClient client, string sub)
    {
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        return client;
    }

    public static HttpClient WithScopes(this HttpClient client, string? scopes)
    {
        if (scopes is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.ScopesHeader, scopes);
        }

        return client;
    }
}
