using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Serilog.Core;

namespace PhoneBook.Identity.IntegrationTests.Infrastructure;

/// <summary>Identity host that records its spans and logs (feature 002, US3).</summary>
public sealed class TelemetryIdentityFactory : IdentityFactory
{
    private readonly ConcurrentQueue<Activity> _activities = new();

    public LogCapture Logs { get; } = new();

    public IReadOnlyList<Activity> Spans => _activities.ToList();

    public async Task<IReadOnlyList<Activity>> WaitForServerSpanAsync(ActivityTraceId traceId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            var spans = _activities.Where(a => a.TraceId == traceId).ToList();
            if (spans.Any(s => s.Kind == ActivityKind.Server) || DateTime.UtcNow > deadline)
            {
                return spans;
            }

            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
    }

    public string AllRecordedText() => string.Join('\n', _activities
        .SelectMany(a => new[] { a.DisplayName, a.StatusDescription ?? string.Empty }
            .Concat(a.TagObjects.Select(t => $"{t.Key}={t.Value}"))
            .Concat(a.Events.Select(e => e.Name)))
        .Concat(Logs.Entries));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.ConfigureOpenTelemetryTracerProvider(b => b.AddProcessor(new CaptureProcessor(_activities)));
            services.AddSingleton<ILogEventSink>(Logs);
        });
    }

    private sealed class CaptureProcessor(ConcurrentQueue<Activity> target) : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data) => target.Enqueue(data);
    }
}
