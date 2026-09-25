using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using PhoneBook.Identity.Data;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace PhoneBook.Identity.Seeding;

/// <summary>Creates the schema, the API scopes and the configured clients. Idempotent.</summary>
internal sealed class IdentitySeeder(IServiceProvider serviceProvider, IOptions<IdentitySettings> settings) : IHostedService
{
    public static readonly string[] ApiScopes = ["phonebook.read", "phonebook.write"];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Opening the keep-alive connection creates the in-memory database before the schema.
        _ = serviceProvider.GetRequiredService<InMemorySqliteKeepAlive>();

        await using var scope = serviceProvider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IdentityDbContext>().Database.EnsureCreatedAsync(cancellationToken);

        var scopeManager = services.GetRequiredService<IOpenIddictScopeManager>();
        foreach (var name in ApiScopes)
        {
            if (await scopeManager.FindByNameAsync(name, cancellationToken) is null)
            {
                await scopeManager.CreateAsync(
                    new OpenIddictScopeDescriptor { Name = name, Resources = { settings.Value.Audience } },
                    cancellationToken);
            }
        }

        var applicationManager = services.GetRequiredService<IOpenIddictApplicationManager>();
        foreach (var client in settings.Value.Clients)
        {
            if (await applicationManager.FindByClientIdAsync(client.ClientId, cancellationToken) is not null)
            {
                continue;
            }

            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = client.ClientId,
                ClientSecret = client.ClientSecret,
                DisplayName = client.DisplayName,
                ClientType = ClientTypes.Confidential,
                Permissions = { Permissions.Endpoints.Token, Permissions.GrantTypes.ClientCredentials },
            };
            foreach (var clientScope in client.Scopes)
            {
                descriptor.Permissions.Add(Permissions.Prefixes.Scope + clientScope);
            }

            await applicationManager.CreateAsync(descriptor, cancellationToken);
        }

        await SeedSwaggerUiClientAsync(applicationManager, cancellationToken);
    }

    /// <summary>
    /// The public first-party client for end-user sign-in (feature 002, data-model §5): no secret, implicit consent,
    /// authorization-code grant with mandatory PKCE, and the configured redirect URIs only.
    /// </summary>
    private async Task SeedSwaggerUiClientAsync(IOpenIddictApplicationManager applicationManager, CancellationToken cancellationToken)
    {
        var client = settings.Value.SwaggerUi;
        if (client.RedirectUris.Count == 0
            || await applicationManager.FindByClientIdAsync(client.ClientId, cancellationToken) is not null)
        {
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = client.ClientId,
            DisplayName = client.DisplayName,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.ResponseTypes.Code,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };
        foreach (var scope in ApiScopes)
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        foreach (var redirectUri in client.RedirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(redirectUri, UriKind.Absolute));
        }

        await applicationManager.CreateAsync(descriptor, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
