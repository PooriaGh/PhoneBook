using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace PhoneBook.Identity.IntegrationTests.Infrastructure;

/// <summary>
/// Serilog sink that keeps rendered messages and property values for personal-data scans (feature 002, FR-016).
/// This project has its own copy because it does not reference the API test project (research R-07).
/// </summary>
public sealed class LogCapture : ILogEventSink
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IEnumerable<string> Entries => _entries;

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        _entries.Enqueue(logEvent.RenderMessage(System.Globalization.CultureInfo.InvariantCulture));
        foreach (var property in logEvent.Properties)
        {
            _entries.Enqueue($"{property.Key}={property.Value}");
        }

        if (logEvent.Exception is not null)
        {
            _entries.Enqueue(logEvent.Exception.ToString());
        }
    }
}
