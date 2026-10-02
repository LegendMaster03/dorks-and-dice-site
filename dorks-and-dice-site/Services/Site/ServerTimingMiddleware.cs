using System.Diagnostics;
using System.Globalization;

namespace dorks_and_dice_site.Services.Site;

public sealed class ServerTimingMiddleware(RequestDelegate next)
{
    public const string HeaderName = "Server-Timing";
    public const string PlatformMetricName = "dnd-site";

    private static readonly object StartedAtKey = new();
    private static readonly object CallbackRegisteredKey = new();

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Items.TryGetValue(StartedAtKey, out var startedAtValue)
            || startedAtValue is not long startedAt)
        {
            startedAt = Stopwatch.GetTimestamp();
            context.Items[StartedAtKey] = startedAt;
        }

        if (!context.Items.ContainsKey(CallbackRegisteredKey))
        {
            context.Items[CallbackRegisteredKey] = true;
            context.Response.OnStarting(() =>
            {
                var durationMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
                var metric = $"{PlatformMetricName};dur={durationMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)}";
                context.Response.Headers.Append(HeaderName, metric);
                return Task.CompletedTask;
            });
        }

        await next(context);
    }
}
