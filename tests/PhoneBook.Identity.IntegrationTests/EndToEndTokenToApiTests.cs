using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Validation;
using PhoneBook.Api;
using PhoneBook.Identity.IntegrationTests.Infrastructure;

namespace PhoneBook.Identity.IntegrationTests;

/// <summary>
/// Real token round-trip: tokens issued by the Identity host are validated by the API's real OpenIddict
/// validation (no TestAuthHandler). The API gets the Identity host's issuer and signing keys from its
/// discovery and JWKS documents, so validation is genuine but needs no network.
/// </summary>
[Collection(IdentityCollection.Name)]
public sealed class EndToEndTokenToApiTests(IdentityFactory identity) : IAsyncLifetime
{
    private static readonly Uri ContactsUri = new("/api/v1/contacts", UriKind.Relative);
    private WebApplicationFactory<ApiAssemblyMarker> _api = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var configuration = await LoadIdentityConfigurationAsync();
        _api = new WebApplicationFactory<ApiAssemblyMarker>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Database:Provider", "Sqlite");
            builder.UseSetting("Database:ConnectionString", $"Data Source=e2e-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            builder.UseSetting("Auth:Authority", IdentityFactory.Issuer);
            builder.ConfigureTestServices(services =>
                services.Configure<OpenIddictValidationOptions>(options => options.Configuration = configuration));
        });
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task SwaggerClientToken_CanCreateContact()
    {
        using var client = await ApiClientWithTokenAsync(TokenClient.SwaggerClientId, TokenClient.SwaggerClientSecret, "phonebook.read phonebook.write");

        var response = await client.PostAsJsonAsync(ContactsUri, NewContact(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ReadOnlyToken_CannotWriteButCanRead()
    {
        using var client = await ApiClientWithTokenAsync(TokenClient.ReadOnlyClientId, TokenClient.ReadOnlyClientSecret, "phonebook.read");

        var post = await client.PostAsJsonAsync(ContactsUri, NewContact(), Ct);
        await AssertProblemAsync(post, HttpStatusCode.Forbidden, "Auth.Forbidden");

        var get = await client.GetAsync(new Uri($"{ContactsUri}?tag=work", UriKind.Relative), Ct);
        get.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NoToken_Returns401ProblemWithWwwAuthenticate()
    {
        using var client = _api.CreateClient();

        var response = await client.PostAsJsonAsync(ContactsUri, NewContact(), Ct);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Auth.Unauthorized");
        response.Headers.WwwAuthenticate.ShouldContain(h => h.Scheme == "Bearer");
    }

    [Fact]
    public async Task MalformedToken_Returns401ProblemWithWwwAuthenticate()
    {
        using var client = _api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "abc");

        var response = await client.GetAsync(new Uri($"{ContactsUri}?tag=work", UriKind.Relative), Ct);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Auth.Unauthorized");
        response.Headers.WwwAuthenticate.ShouldContain(h => h.Scheme == "Bearer");
    }

    private async Task<HttpClient> ApiClientWithTokenAsync(string clientId, string secret, string scope)
    {
        using var identityClient = identity.CreateClient();
        var token = await TokenClient.GetAccessTokenAsync(identityClient, clientId, secret, scope);
        var client = _api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<OpenIddictConfiguration> LoadIdentityConfigurationAsync()
    {
        using var client = identity.CreateClient();
        var discovery = await client.GetFromJsonAsync<JsonElement>("/.well-known/openid-configuration", Ct);
        var jwksUri = new Uri(discovery.GetProperty("jwks_uri").GetString()!);
        var jwks = new JsonWebKeySet(await client.GetStringAsync(jwksUri.PathAndQuery, Ct));

        var configuration = new OpenIddictConfiguration { Issuer = new Uri(discovery.GetProperty("issuer").GetString()!) };
        foreach (var key in jwks.GetSigningKeys())
        {
            configuration.SigningKeys.Add(key);
        }

        return configuration;
    }

    private static object NewContact() => new
    {
        FirstName = "علی",
        LastName = "رضایی",
        PhoneNumber = $"0912{Random.Shared.Next(1000000, 9999999)}",
        Tag = "work",
    };

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string errorCode)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("errorCode").GetString().ShouldBe(errorCode);
        body.TryGetProperty("traceId", out _).ShouldBeTrue();
    }
}
