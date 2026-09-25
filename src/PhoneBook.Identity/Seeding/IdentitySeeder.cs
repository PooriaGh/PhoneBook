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
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
