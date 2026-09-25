using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog.Core;

namespace PhoneBook.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Captures the spans, metrics and logs of one test host (feature 002, research R-07).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Activity sources are process-wide, so a host's tracer also sees other hosts' spans. Tests therefore read
/// only their own traces through <see cref="ForTrace"/>.</item>
/// <item>A filtering processor keeps only server spans, spans with a parent, and PhoneBook spans. Root spans from
/// elsewhere, such as the per-row database commands of test seeding, are dropped (analyze P1).</item>
/// </list>
/// </remarks>
public sealed class TelemetryCapture
{
    private readonly ConcurrentQueue<Activity> _activities = new();
    private readonly List<Metric> _metrics = [];

    public LogCapture Logs { get; } = new();

    public void AddTo(IServiceCollection services)
    {
        services.ConfigureOpenTelemetryTracerProvider(builder => builder.AddProcessor(new CaptureProcessor(this)));
        services.ConfigureOpenTelemetryMeterProvider(builder => builder.AddInMemoryExporter(_metrics));
        services.AddSingleton<ILogEventSink>(Logs);
    }

    public IReadOnlyList<Activity> AllSpans() => _activities.ToList();

    public IReadOnlyList<Activity> ForTrace(ActivityTraceId traceId) =>
        _activities.Where(a => a.TraceId == traceId).ToList();

    /// <summary>Waits until the server span of <paramref name="traceId"/> has been recorded (it ends after the response).</summary>
    public async Task<IReadOnlyList<Activity>> WaitForTraceAsync(ActivityTraceId traceId, Func<IReadOnlyList<Activity>, bool>? until = null)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            var spans = ForTrace(traceId);
            if ((until ?? HasServerSpan)(spans) || DateTime.UtcNow > deadline)
            {
                return spans;
            }

            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
    }

    public static bool HasServerSpan(IReadOnlyList<Activity> spans) => spans.Any(s => s.Kind == ActivityKind.Server);

    public static void Flush(IServiceProvider services)
    {
        services.GetService<TracerProvider>()?.ForceFlush();
        services.GetService<MeterProvider>()?.ForceFlush();
    }

    /// <summary>Everything recorded in spans, metrics and logs, for the personal-data scan (SC-006).</summary>
    public string AllRecordedText()
    {
        var text = new List<string>();
        foreach (var activity in _activities)
        {
            text.Add(activity.DisplayName);
            text.Add(activity.OperationName);
            text.Add(activity.StatusDescription ?? string.Empty);
            text.AddRange(activity.TagObjects.Select(t => $"{t.Key}={t.Value}"));
            text.AddRange(activity.Baggage.Select(b => $"{b.Key}={b.Value}"));
            foreach (var activityEvent in activity.Events)
            {
                text.Add(activityEvent.Name);
                text.AddRange(activityEvent.Tags.Select(t => $"{t.Key}={t.Value}"));
            }
        }

        lock (_metrics)
        {
            foreach (var metric in _metrics)
            {
                text.Add(metric.Name);
                foreach (ref readonly var point in metric.GetMetricPoints())
                {
                    foreach (var tag in point.Tags)
                    {
                        text.Add($"{tag.Key}={tag.Value}");
                    }
                }
            }
        }

        text.AddRange(Logs.Entries);
        return string.Join('\n', text);
    }

    private sealed class CaptureProcessor(TelemetryCapture capture) : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data)
        {
            var keep = data.Kind == ActivityKind.Server
                || data.ParentSpanId != default
                || data.Source.Name.StartsWith("PhoneBook.", StringComparison.Ordinal);
            if (keep)
            {
                capture._activities.Enqueue(data);
            }
        }
    }
}
