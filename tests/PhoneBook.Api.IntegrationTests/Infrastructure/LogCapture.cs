using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace PhoneBook.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Serilog sink that keeps rendered messages and property values for the personal-data scan (feature 002, FR-016).
/// Registered as <see cref="ILogEventSink"/> in DI, where the host's <c>ReadFrom.Services</c> picks it up.
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
