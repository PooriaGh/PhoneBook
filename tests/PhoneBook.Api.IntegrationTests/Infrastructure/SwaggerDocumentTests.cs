using System.Net.Http.Json;
using System.Text.Json;

namespace PhoneBook.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Feature 002, FR-022: the interactive documentation offers the end-user authorization-code flow next to client
/// credentials, and describes the paged tag-search response.
/// </summary>
[Collection(SqliteIntegrationTestCollection.Name)]
public sealed class SwaggerDocumentTests(SqliteApiFactory factory)
{
    [Fact]
    public async Task Document_HasBothOAuthFlowsAndPagedTagSearch()
    {
        using var client = factory.CreateClient();

        var document = await client.GetFromJsonAsync<JsonElement>(
            new Uri("/swagger/v1/swagger.json", UriKind.Relative), TestContext.Current.CancellationToken);

        var flows = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("oauth2").GetProperty("flows");
        flows.TryGetProperty("clientCredentials", out _).ShouldBeTrue();
        var code = flows.GetProperty("authorizationCode");
        code.GetProperty("authorizationUrl").GetString().ShouldEndWith("/connect/authorize");
        code.GetProperty("tokenUrl").GetString().ShouldEndWith("/connect/token");

        var ok = document.GetProperty("paths").GetProperty("/api/v1/contacts").GetProperty("get")
            .GetProperty("responses").GetProperty("200").GetRawText();
        ok.ShouldContain("PagedResponse");
    }
}
