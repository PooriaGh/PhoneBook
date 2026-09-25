using System.Net.Http.Json;
using System.Text.Json;

namespace PhoneBook.Identity.IntegrationTests.Infrastructure;

/// <summary>Development-only client credentials (appsettings.Development.json of PhoneBook.Identity).</summary>
internal static class TokenClient
{
    public const string SwaggerClientId = "phonebook-swagger";
    public const string SwaggerClientSecret = "phonebook-swagger-dev-secret";
    public const string ReadOnlyClientId = "phonebook-readonly";
    public const string ReadOnlyClientSecret = "phonebook-readonly-dev-secret";

    public static Task<HttpResponseMessage> RequestAsync(
        HttpClient client, string clientId, string clientSecret, string? scope, string grantType = "client_credentials")
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = grantType,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        };
        if (scope is not null)
        {
            form["scope"] = scope;
        }

        return client.PostAsync(new Uri("/connect/token", UriKind.Relative), new FormUrlEncodedContent(form), TestContext.Current.CancellationToken);
    }

    public static async Task<string> GetAccessTokenAsync(HttpClient client, string clientId, string clientSecret, string scope)
    {
        var response = await RequestAsync(client, clientId, clientSecret, scope);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("access_token").GetString()!;
    }
}
