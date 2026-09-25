using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using PhoneBook.Identity.IntegrationTests.Infrastructure;

namespace PhoneBook.Identity.IntegrationTests;

/// <summary>
/// Feature 002, US3 on the Identity host: request spans, the identity half of the cross-service trace link (FR-012),
/// no personal or secret data in spans or logs (FR-016), and the token-policy rejection metric.
/// </summary>
public sealed class TelemetryTests : IAsyncLifetime
{
    private readonly TelemetryIdentityFactory _factory = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task TokenRequest_IsTracedWithoutSecrets()
    {
        using var client = _factory.CreateClient();
        var traceId = ActivityTraceId.CreateRandom();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/connect/token", UriKind.Relative))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = TokenClient.SwaggerClientId,
                ["client_secret"] = TokenClient.SwaggerClientSecret,
                ["scope"] = "phonebook.read",
            }),
        };
        request.Headers.Add("traceparent", $"00-{traceId.ToHexString()}-{ActivitySpanId.CreateRandom().ToHexString()}-01");

        var response = await client.SendAsync(request, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var accessToken = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("access_token").GetString()!;

        // Sign-in attempts (right and wrong password) must not leak either (the form arrives in US4).
        foreach (var password in new[] { "alice-dev-password", "wrong-password" })
        {
            await client.PostAsync(
                new Uri("/account/login", UriKind.Relative),
                new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = "alice", ["password"] = password }),
                Ct);
        }

        var spans = await _factory.WaitForServerSpanAsync(traceId);
        spans.ShouldContain(s => s.Kind == ActivityKind.Server && (s.GetTagItem("url.path") as string) == "/connect/token");

        await Task.Delay(200, Ct);
        var recorded = _factory.AllRecordedText();
        recorded.ShouldNotBeEmpty();
        foreach (var value in new[]
                 {
                     TokenClient.SwaggerClientSecret, "client_secret=", accessToken, "alice", "alice-dev-password", "wrong-password",
                     "127.0.0.1",
                 })
        {
            recorded.Contains(value, StringComparison.OrdinalIgnoreCase).ShouldBeFalse($"'{value}' leaked into telemetry or logs");
        }
    }

    [Fact]
    public async Task IncomingTraceparent_IsContinued()
    {
        using var client = _factory.CreateClient();
        var traceId = ActivityTraceId.CreateRandom();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/.well-known/openid-configuration", UriKind.Relative));
        request.Headers.Add("traceparent", $"00-{traceId.ToHexString()}-{ActivitySpanId.CreateRandom().ToHexString()}-01");

        (await client.SendAsync(request, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var spans = await _factory.WaitForServerSpanAsync(traceId);
        spans.ShouldContain(s => s.Kind == ActivityKind.Server, "the Identity host must continue the caller's trace");
    }

    [Fact]
    public async Task TokenRateLimitRejection_IsCounted()
    {
        await using var limited = new RateLimitedIdentityFactory();
        using var rejections = new MetricCollector<long>(
            limited.Services.GetRequiredService<IMeterFactory>(), "PhoneBook", "phonebook.ratelimit.rejections");
        using var client = limited.CreateClient();

        for (var i = 0; i <= RateLimitedIdentityFactory.Limit; i++)
        {
            await TokenClient.RequestAsync(client, TokenClient.SwaggerClientId, TokenClient.SwaggerClientSecret, "phonebook.read");
        }

        var snapshot = rejections.GetMeasurementSnapshot();
        snapshot.Sum(m => m.Value).ShouldBe(1);
        snapshot.Single().Tags["policy"].ShouldBe("token");
    }
}
