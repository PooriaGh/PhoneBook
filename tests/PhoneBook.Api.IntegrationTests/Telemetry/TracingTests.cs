using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using static PhoneBook.Api.IntegrationTests.Infrastructure.HttpAssertions;

namespace PhoneBook.Api.IntegrationTests.Telemetry;

/// <summary>
/// Feature 002, US3: one trace per request covering HTTP, the MediatR request and persistence (FR-012), and the
/// ProblemDetails <c>traceId</c> equal to the recorded trace (FR-013, SC-004). Runs on both providers.
/// </summary>
public abstract partial class TracingTestsBase(IPhoneBookApiFactory factory, string dbSystem) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task CreateContact_RecordsOneTraceWithHttpApplicationAndPersistenceSpans()
    {
        var traceId = ActivityTraceId.CreateRandom();
        using var request = new HttpRequestMessage(HttpMethod.Post, ContactsUri())
        {
            Content = JsonContent.Create(ContactFaker.Request()),
        };
        request.Headers.Add("traceparent", $"00-{traceId.ToHexString()}-{ActivitySpanId.CreateRandom().ToHexString()}-01");

        var response = await Client.SendAsync(request, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var spans = await Factory.Capture.WaitForTraceAsync(traceId);
        var server = spans.Single(s => s.Kind == ActivityKind.Server);
        server.GetTagItem("http.route").ShouldNotBeNull();

        var command = spans.Single(s => s.Source.Name == "PhoneBook.Application" && s.DisplayName == "CreateContactCommand");
        command.GetTagItem("phonebook.result").ShouldBe("success");
        ReachesAncestor(spans, command, server).ShouldBeTrue("the command span must descend from the server span");

        var save = spans.Single(s => s.Source.Name == "PhoneBook.Persistence" && s.DisplayName == "save");
        save.GetTagItem("db.operation").ShouldBe("save");
        save.GetTagItem("db.system").ShouldBe(dbSystem);

        if (dbSystem == "postgresql")
        {
            spans.ShouldContain(s => s.Source.Name == "Npgsql", "PostgreSQL commands are traced natively");
        }
    }

    [Fact]
    public async Task ErrorResponses_TraceIdEqualsRecordedTrace()
    {
        await SeedContactAsync("Ali", "Rezaei", "09121234567", "work");

        var notFound = await Client.GetAsync(ContactsUri($"/{Guid.NewGuid()}"), Ct);
        var badRequest = await Client.GetAsync(ContactsUri("?tag="), Ct);
        var conflict = await Client.PostAsJsonAsync(
            ContactsUri(), new ContactRequestData("Ali", "Rezaei", "09121234567", "work"), Ct);

        foreach (var (response, status) in new[]
                 {
                     (notFound, HttpStatusCode.NotFound), (badRequest, HttpStatusCode.BadRequest), (conflict, HttpStatusCode.Conflict),
                 })
        {
            response.StatusCode.ShouldBe(status);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            var traceId = body.GetProperty("traceId").GetString()!;
            TraceIdPattern().IsMatch(traceId).ShouldBeTrue($"'{traceId}' is not a 32-character W3C trace id");

            var spans = await Factory.Capture.WaitForTraceAsync(ActivityTraceId.CreateFromString(traceId));
            spans.ShouldContain(s => s.Kind == ActivityKind.Server, $"no server span recorded for trace {traceId}");
        }
    }

    [Fact]
    public async Task FailedCommand_SpanCarriesErrorCodeAndErrorStatus()
    {
        await SeedContactAsync("Ali", "Rezaei", "09121234567", "work");

        var conflict = await Client.PostAsJsonAsync(
            ContactsUri(), new ContactRequestData("Ali", "Rezaei", "09121234567", "work"), Ct);
        var traceId = (await conflict.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("traceId").GetString()!;

        var spans = await Factory.Capture.WaitForTraceAsync(ActivityTraceId.CreateFromString(traceId));
        var command = spans.Single(s => s.DisplayName == "CreateContactCommand");
        command.GetTagItem("phonebook.result").ShouldBe("Contact.Duplicate");
        command.Status.ShouldBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task TagSearch_RecordsQueryPersistenceSpan()
    {
        var traceId = ActivityTraceId.CreateRandom();
        using var request = new HttpRequestMessage(HttpMethod.Get, ContactsUri("?tag=work&page=1"));
        request.Headers.Add("traceparent", $"00-{traceId.ToHexString()}-{ActivitySpanId.CreateRandom().ToHexString()}-01");

        (await Client.SendAsync(request, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var spans = await Factory.Capture.WaitForTraceAsync(traceId);
        var query = spans.Single(s => s.Source.Name == "PhoneBook.Persistence" && s.DisplayName == "query.contacts_by_tag");
        query.GetTagItem("db.operation").ShouldBe("query.contacts_by_tag");
        query.GetTagItem("db.system").ShouldBe(dbSystem);
    }

    private static bool ReachesAncestor(IReadOnlyList<Activity> spans, Activity span, Activity ancestor)
    {
        var bySpanId = spans.ToDictionary(s => s.SpanId);
        var current = span;
        for (var depth = 0; depth < 32; depth++)
        {
            if (current.ParentSpanId == ancestor.SpanId)
            {
                return true;
            }

            if (!bySpanId.TryGetValue(current.ParentSpanId, out var parent))
            {
                return false;
            }

            current = parent;
        }

        return false;
    }

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex TraceIdPattern();
}

[Collection(IntegrationTestCollection.Name)]
public sealed class PostgresTracingTests(PhoneBookApiFactory factory) : TracingTestsBase(factory, "postgresql");

[Collection(SqliteIntegrationTestCollection.Name)]
public sealed class SqliteTracingTests(SqliteApiFactory factory) : TracingTestsBase(factory, "sqlite");
