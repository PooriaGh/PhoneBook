using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PhoneBook.Api.IntegrationTests.Infrastructure;

internal static class HttpAssertions
{
    public const string ContactsPath = "/api/v1/contacts";

    public static Uri ContactsUri(string suffix = "") => new($"{ContactsPath}{suffix}", UriKind.Relative);

    /// <summary>Asserts an RFC 9457 problem response and returns its parsed body.</summary>
    public static async Task<JsonElement> ShouldBeProblemAsync(
        this HttpResponseMessage response, HttpStatusCode status, string errorCode)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("status").GetInt32().ShouldBe((int)status);
        body.GetProperty("errorCode").GetString().ShouldBe(errorCode);
        body.TryGetProperty("traceId", out _).ShouldBeTrue("problem responses must carry a traceId");
        return body;
    }

    public static string[] ErrorFields(this JsonElement problem) =>
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToArray();
}
