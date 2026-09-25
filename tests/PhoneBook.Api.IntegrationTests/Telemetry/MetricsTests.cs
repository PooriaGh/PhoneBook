using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using PhoneBook.Application.Contacts.Delete;
using PhoneBook.Application.Contacts.Update;
using static PhoneBook.Api.IntegrationTests.Infrastructure.HttpAssertions;

namespace PhoneBook.Api.IntegrationTests.Telemetry;

/// <summary>
/// Feature 002, US3 AC3 (FR-014): request and domain metrics. Collectors are bound to this host's
/// <see cref="IMeterFactory"/>, so they see only this host's measurements (research R-07).
/// </summary>
[Collection(SqliteIntegrationTestCollection.Name)]
public sealed class MetricsTests(SqliteApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task ContactLifecycle_IncrementsDomainCounters()
    {
        var meterFactory = Factory.Services.GetRequiredService<IMeterFactory>();
        using var created = new MetricCollector<long>(meterFactory, "PhoneBook", "phonebook.contacts.created");
        using var updated = new MetricCollector<long>(meterFactory, "PhoneBook", "phonebook.contacts.updated");
        using var deleted = new MetricCollector<long>(meterFactory, "PhoneBook", "phonebook.contacts.deleted");

        var response = await Client.PostAsJsonAsync(ContactsUri(), ContactFaker.Request(tag: "work"), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        (await Sender.Send(new UpdateContactCommand(id, "Ali", "Rezaei", "09121234567", "work", null), Ct)).IsSuccess.ShouldBeTrue();
        (await Sender.Send(new DeleteContactCommand(id, null), Ct)).IsSuccess.ShouldBeTrue();

        created.GetMeasurementSnapshot().Sum(m => m.Value).ShouldBe(1);
        updated.GetMeasurementSnapshot().Sum(m => m.Value).ShouldBe(1);
        deleted.GetMeasurementSnapshot().Sum(m => m.Value).ShouldBe(1);
    }

    [Fact]
    public async Task Requests_RecordDurationWithRouteStatusAndMethod()
    {
        var meterFactory = Factory.Services.GetRequiredService<IMeterFactory>();
        using var duration = new MetricCollector<double>(meterFactory, "Microsoft.AspNetCore.Hosting", "http.server.request.duration");

        (await Client.GetAsync(ContactsUri("?tag=work"), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await duration.WaitForMeasurementsAsync(1, TimeSpan.FromSeconds(5));

        var snapshot = duration.GetMeasurementSnapshot();
        var measurement = snapshot[^1];
        measurement.Tags.ShouldContainKey("http.route");
        measurement.Tags["http.response.status_code"].ShouldBe(200);
        measurement.Tags["http.request.method"].ShouldBe("GET");
    }

    [Fact]
    public async Task RateLimitRejection_IsCountedAndTraced()
    {
        await using var limited = new RateLimitedApiFactory();
        using var rejections = new MetricCollector<long>(
            limited.Services.GetRequiredService<IMeterFactory>(), "PhoneBook", "phonebook.ratelimit.rejections");
        using var client = limited.CreateClientWithScopes().WithSub($"metrics-{Guid.NewGuid():N}");

        HttpResponseMessage? rejected = null;
        for (var i = 0; i <= RateLimitedApiFactory.PermitLimit; i++)
        {
            rejected = await client.GetAsync(ContactsUri("?tag=work"), Ct);
        }

        rejected!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        var snapshot = rejections.GetMeasurementSnapshot();
        snapshot.Sum(m => m.Value).ShouldBe(1);
        snapshot.Single().Tags["policy"].ShouldBe("api");

        // Spec edge case: a rejected request still gets a trace (analyze G2).
        var traceId = (await rejected.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("traceId").GetString()!;
        var spans = await limited.Capture.WaitForTraceAsync(ActivityTraceId.CreateFromString(traceId));
        spans.ShouldContain(s => s.Kind == ActivityKind.Server && Equals(s.GetTagItem("http.response.status_code"), 429));
    }
}
