using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using static PhoneBook.Api.IntegrationTests.Infrastructure.HttpAssertions;

namespace PhoneBook.Api.IntegrationTests.Telemetry;

/// <summary>
/// Feature 002, US3 AC4 / FR-017: an unreachable telemetry destination never fails or blocks requests. The 5% latency
/// comparison of SC-005 is measured manually (quickstart #13); CI asserts only that nothing blocks (research R-04).
/// </summary>
[Trait("Category", "Performance")]
public sealed class TelemetryResilienceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UnreachableExporter_RequestsStillSucceedWithoutBlocking()
    {
        await using var baseFactory = new SqliteApiFactory();
        await using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            var unreachable = new Uri("http://127.0.0.1:9");
            services.ConfigureOpenTelemetryTracerProvider(b => b.AddOtlpExporter(o => o.Endpoint = unreachable));
            services.ConfigureOpenTelemetryMeterProvider(b => b.AddOtlpExporter(o => o.Endpoint = unreachable));
        }));

        await RunMixedTrafficAsync(factory.CreateClient().WithScopes(null));
    }

    [Fact]
    public async Task ExportDisabled_RequestsSucceed()
    {
        await using var factory = new SqliteApiFactory();

        await RunMixedTrafficAsync(factory.CreateClientWithScopes());
    }

    private static async Task RunMixedTrafficAsync(HttpClient client)
    {
        using (client)
        {
            for (var i = 0; i < 25; i++)
            {
                var create = await TimedAsync(() => client.PostAsJsonAsync(ContactsUri(), ContactFaker.Request(tag: "resilience"), Ct));
                create.StatusCode.ShouldBe(HttpStatusCode.Created);

                var search = await TimedAsync(() => client.GetAsync(ContactsUri("?tag=resilience"), Ct));
                search.StatusCode.ShouldBe(HttpStatusCode.OK);
            }
        }
    }

    private static async Task<HttpResponseMessage> TimedAsync(Func<Task<HttpResponseMessage>> send)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await send();
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1), "a request must never wait on telemetry export");
        return response;
    }
}
