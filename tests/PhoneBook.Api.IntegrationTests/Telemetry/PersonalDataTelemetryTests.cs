using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PhoneBook.Api.IntegrationTests.Infrastructure;
using PhoneBook.Application.Contacts.Update;
using static PhoneBook.Api.IntegrationTests.Infrastructure.HttpAssertions;

namespace PhoneBook.Api.IntegrationTests.Telemetry;

/// <summary>
/// Feature 002, FR-016 / SC-006 / US3 AC5: traffic with distinctive personal values leaves no trace of them in spans,
/// metrics or logs. The scan covers everything the host captured, not only this test's traces. Runs on both
/// providers.
/// </summary>
public abstract class PersonalDataTelemetryTestsBase(IPhoneBookApiFactory factory) : BaseIntegrationTest(factory)
{
    private const string FirstName = "Zyxwvfirst";
    private const string LastName = "Qponmlast";
    private const string Phone = "09129998877";
    private const string Tag = "tag-qqzz-secret";
    private const string PersianName = "ژاله‌تست";

    [Fact]
    public async Task Telemetry_ContainsNoPersonalData()
    {
        var created = await Client.PostAsJsonAsync(ContactsUri(), new ContactRequestData(FirstName, LastName, Phone, Tag), Ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();

        (await Client.GetAsync(ContactsUri($"/{id}"), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Client.GetAsync(ContactsUri($"?tag={Uri.EscapeDataString(Tag)}&page=1&pageSize=10"), Ct)).StatusCode
            .ShouldBe(HttpStatusCode.OK);
        (await Sender.Send(new UpdateContactCommand(id, PersianName, LastName, Phone, Tag, null), Ct)).IsSuccess.ShouldBeTrue();
        (await Client.PostAsJsonAsync(ContactsUri(), new ContactRequestData(FirstName, new string('x', 101), Phone, Tag), Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Client.PostAsJsonAsync(ContactsUri(), new ContactRequestData(FirstName, LastName, Phone, Tag), Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Client.DeleteAsync(ContactsUri($"/{id}"), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await Task.Delay(200, Ct); // let the last server spans end
        TelemetryCapture.Flush(Factory.Services);
        var recorded = Factory.Capture.AllRecordedText();

        recorded.ShouldNotBeEmpty("telemetry must actually be recorded for this scan to mean anything");
        // Client addresses are checked through the client.address tag below. Server-side peer addresses (for example the
        // PostgreSQL container's 127.0.0.1 on database spans) are infrastructure data, not personal data.
        foreach (var value in new[] { FirstName, LastName, Phone, Tag, PersianName })
        {
            recorded.Contains(value, StringComparison.OrdinalIgnoreCase).ShouldBeFalse($"'{value}' leaked into telemetry or logs");
        }

        var spans = Factory.Capture.AllSpans();
        spans.ShouldNotContain(s => s.GetTagItem("client.address") != null, "client.address must be removed");
        spans.ShouldNotContain(
            s => (s.GetTagItem("url.query") as string ?? string.Empty).Contains("tag=", StringComparison.Ordinal),
            "url.query must be removed");
    }
}

[Collection(IntegrationTestCollection.Name)]
public sealed class PostgresPersonalDataTelemetryTests(PhoneBookApiFactory factory) : PersonalDataTelemetryTestsBase(factory);

[Collection(SqliteIntegrationTestCollection.Name)]
public sealed class SqlitePersonalDataTelemetryTests(SqliteApiFactory factory) : PersonalDataTelemetryTestsBase(factory);
