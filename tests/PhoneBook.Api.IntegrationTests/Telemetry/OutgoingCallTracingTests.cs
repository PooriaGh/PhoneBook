using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using PhoneBook.Application.Abstractions.Telemetry;

namespace PhoneBook.Api.IntegrationTests.Telemetry;

/// <summary>
/// Feature 002, FR-012 (clarified): the phone book service's outgoing calls, such as fetching the identity service's
/// discovery document, are traced and carry the trace context. This is the API half of the cross-service link; the
/// Identity half is in the Identity tests, and the live view is quickstart #11 (research R-04).
/// </summary>
/// <remarks>In-process TestServer handlers bypass .NET's HTTP diagnostics, so a real socket handler is used.</remarks>
[Collection(SqliteIntegrationTestCollection.Name)]
public sealed class OutgoingCallTracingTests(SqliteApiFactory factory)
{
    [Fact]
    public async Task OutgoingHttpCall_IsTracedAsChildOfCurrentSpan()
    {
        var httpClientFactory = factory.Services.GetRequiredService<IHttpClientFactory>();

        using var parent = PhoneBookTelemetry.Application.StartActivity("test.outgoing");
        parent.ShouldNotBeNull("the host's tracer must listen to PhoneBook.Application");

        using var client = httpClientFactory.CreateClient();
        await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync(
            new Uri("http://127.0.0.1:9/.well-known/openid-configuration"), TestContext.Current.CancellationToken));

        var spans = await factory.Capture.WaitForTraceAsync(
            parent.TraceId, until: s => s.Any(a => a.Kind == ActivityKind.Client));
        var call = spans.Single(s => s.Kind == ActivityKind.Client);
        call.ParentSpanId.ShouldBe(parent.SpanId);
        call.GetTagItem("http.request.method").ShouldBe("GET");
        call.GetTagItem("server.address").ShouldNotBeNull();
        call.GetTagItem("url.query").ShouldBeNull();
    }
}
