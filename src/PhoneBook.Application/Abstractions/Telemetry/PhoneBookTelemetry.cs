using System.Diagnostics;

namespace PhoneBook.Application.Abstractions.Telemetry;

/// <summary>
/// Telemetry names shared by the layers (feature 002, data-model §3). Only BCL <see cref="System.Diagnostics"/>
/// types are used here; exporters and instrumentation are wired up by the hosts.
/// </summary>
/// <remarks>
/// Activity sources are process-wide on purpose: spans are correlated by trace id. Counters are not defined here,
/// because they come from the host's <c>IMeterFactory</c> (<see cref="PhoneBookMetrics"/>).
/// </remarks>
public static class PhoneBookTelemetry
{
    public const string ApplicationSourceName = "PhoneBook.Application";
    public const string PersistenceSourceName = "PhoneBook.Persistence";
    public const string MeterName = "PhoneBook";

    public const string RequestTag = "phonebook.request";
    public const string ResultTag = "phonebook.result";
    public const string DbSystemTag = "db.system";
    public const string DbOperationTag = "db.operation";
    public const string PolicyTag = "policy";

    public static readonly ActivitySource Application = new(ApplicationSourceName);

    public static readonly ActivitySource Persistence = new(PersistenceSourceName);
}
