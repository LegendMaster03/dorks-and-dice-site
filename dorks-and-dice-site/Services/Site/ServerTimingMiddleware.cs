using System.Diagnostics;
using System.Globalization;

namespace dorks_and_dice_site.Services.Site;

public sealed class ServerTimingMiddleware(RequestDelegate next)
{
    public const string HeaderName = "Server-Timing";
    public const string SiteMetricName = "dnd-site";
    public const string ToolMetricName = "dnd-tool";
    public const string TotalMetricName = "dnd-total";

    // Retained for callers/tests that used the original platform metric constant.
    public const string PlatformMetricName = SiteMetricName;

    private static readonly object RequestStateKey = new();

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Items.TryGetValue(RequestStateKey, out var existing)
            || existing is not RequestTimingState)
        {
            var state = new RequestTimingState(Stopwatch.GetTimestamp());
            context.Items[RequestStateKey] = state;
            context.Response.OnStarting(() =>
            {
                EmitPlatformTiming(context, state);
                return Task.CompletedTask;
            });
        }

        await next(context);
    }

    internal static IDisposable? BeginToolTiming(HttpContext context, string toolKey)
    {
        if (!context.Items.TryGetValue(RequestStateKey, out var existing)
            || existing is not RequestTimingState state
            || string.IsNullOrWhiteSpace(toolKey))
        {
            return null;
        }

        return new ToolTimingScope(state, toolKey.Trim(), Stopwatch.GetTimestamp());
    }

    private static void EmitPlatformTiming(HttpContext context, RequestTimingState state)
    {
        var endedAt = Stopwatch.GetTimestamp();
        var intervals = state.Snapshot();
        var totalTicks = Math.Max(0, endedAt - state.StartedAt);
        var toolWaitTicks = CalculateUnionTicks(intervals, state.StartedAt, endedAt);
        var siteTicks = Math.Max(0, totalTicks - toolWaitTicks);

        AppendMetric(context, SiteMetricName, ToMilliseconds(siteTicks));

        foreach (var interval in intervals)
        {
            var clippedStart = Math.Max(state.StartedAt, interval.StartedAt);
            var clippedEnd = Math.Min(endedAt, interval.EndedAt);
            if (clippedEnd <= clippedStart)
            {
                continue;
            }

            AppendMetric(
                context,
                ToolMetricName,
                ToMilliseconds(clippedEnd - clippedStart),
                interval.ToolKey);
        }

        AppendMetric(context, TotalMetricName, ToMilliseconds(totalTicks));
    }

    private static long CalculateUnionTicks(
        IReadOnlyList<ToolTimingInterval> intervals,
        long requestStartedAt,
        long requestEndedAt)
    {
        var ordered = intervals
            .Select(interval => new
            {
                StartedAt = Math.Max(requestStartedAt, interval.StartedAt),
                EndedAt = Math.Min(requestEndedAt, interval.EndedAt)
            })
            .Where(interval => interval.EndedAt > interval.StartedAt)
            .OrderBy(interval => interval.StartedAt)
            .ToArray();

        if (ordered.Length == 0)
        {
            return 0;
        }

        long total = 0;
        var currentStart = ordered[0].StartedAt;
        var currentEnd = ordered[0].EndedAt;

        for (var index = 1; index < ordered.Length; index++)
        {
            var interval = ordered[index];
            if (interval.StartedAt <= currentEnd)
            {
                currentEnd = Math.Max(currentEnd, interval.EndedAt);
                continue;
            }

            total += currentEnd - currentStart;
            currentStart = interval.StartedAt;
            currentEnd = interval.EndedAt;
        }

        return total + (currentEnd - currentStart);
    }

    private static void AppendMetric(
        HttpContext context,
        string metricName,
        double durationMilliseconds,
        string? description = null)
    {
        var duration = durationMilliseconds.ToString("0.###", CultureInfo.InvariantCulture);
        var metric = string.IsNullOrEmpty(description)
            ? $"{metricName};dur={duration}"
            : $"{metricName};desc=\"{EscapeDescription(description)}\";dur={duration}";
        context.Response.Headers.Append(HeaderName, metric);
    }

    private static string EscapeDescription(string description) =>
        description
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);

    private static double ToMilliseconds(long stopwatchTicks) =>
        stopwatchTicks * 1000d / Stopwatch.Frequency;

    private sealed class RequestTimingState(long startedAt)
    {
        private readonly object _gate = new();
        private readonly List<ToolTimingInterval> _toolIntervals = [];

        public long StartedAt { get; } = startedAt;

        public void AddToolInterval(ToolTimingInterval interval)
        {
            lock (_gate)
            {
                _toolIntervals.Add(interval);
            }
        }

        public IReadOnlyList<ToolTimingInterval> Snapshot()
        {
            lock (_gate)
            {
                return _toolIntervals.ToArray();
            }
        }
    }

    private sealed class ToolTimingScope(
        RequestTimingState state,
        string toolKey,
        long startedAt) : IDisposable
    {
        private int _completed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _completed, 1) != 0)
            {
                return;
            }

            state.AddToolInterval(new ToolTimingInterval(
                toolKey,
                startedAt,
                Stopwatch.GetTimestamp()));
        }
    }

    private readonly record struct ToolTimingInterval(
        string ToolKey,
        long StartedAt,
        long EndedAt);
}
