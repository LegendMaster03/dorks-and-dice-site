using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace dorks_and_dice_site.Services.Tools;

public sealed class ToolProxyRequestBodyLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ToolProxyOptions _options;
    private readonly IToolRegistry _toolRegistry;

    public ToolProxyRequestBodyLimitMiddleware(
        RequestDelegate next,
        IOptions<ToolProxyOptions> options,
        IToolRegistry toolRegistry)
    {
        _next = next;
        _options = options.Value;
        _toolRegistry = toolRegistry;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!await EnforceReleaseAudienceAsync(context))
        {
            return;
        }

        if (!IsToolHostUpstreamRequest(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var requestSizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (requestSizeFeature is { IsReadOnly: false })
        {
            requestSizeFeature.MaxRequestBodySize = _options.MaxRequestBodyBytes;
        }

        if (context.Request.ContentLength is long contentLength
            && contentLength > _options.MaxRequestBodyBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        try
        {
            await _next(context);
        }
        catch (BadHttpRequestException exception)
            when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge
                && !context.Response.HasStarted)
        {
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        }
    }

    private async Task<bool> EnforceReleaseAudienceAsync(HttpContext context)
    {
        if (!TryGetUserFacingApplicationSlug(context.Request.Path, out var slug))
        {
            return true;
        }

        var tool = await _toolRegistry.GetBySlugAsync(slug, context.RequestAborted);
        if (tool is null
            || tool.Kind != ToolKind.Application
            || !tool.Enabled
            || tool.ReleaseAudience == ToolReleaseAudience.Public)
        {
            return true;
        }

        var modeId = context.GetSiteModeContext().ActiveModeId;
        if (!ToolVisibility.IsVisibleInMode(tool, modeId))
        {
            return true;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await context.ChallengeAsync();
            return false;
        }

        if (ToolVisibility.CanUseReleaseAudience(tool, modeId, context.User))
        {
            return true;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return false;
    }

    private static bool TryGetUserFacingApplicationSlug(PathString path, out string slug)
    {
        slug = string.Empty;
        var value = path.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2
            && (string.Equals(segments[0], "tools", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segments[0], "tool-modules", StringComparison.OrdinalIgnoreCase)))
        {
            slug = segments[1];
            return !string.IsNullOrWhiteSpace(slug);
        }

        if (segments.Length < 2
            || !string.Equals(segments[0], "tool-host", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segments[1], "registrations", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (segments.Length >= 4
            && string.Equals(segments[2], "api", StringComparison.OrdinalIgnoreCase)
            && IsBackendCapabilityRoute(segments[3]))
        {
            return false;
        }

        slug = segments[1];
        return !string.IsNullOrWhiteSpace(slug);
    }

    private static bool IsBackendCapabilityRoute(string segment) =>
        string.Equals(segment, "introspect", StringComparison.OrdinalIgnoreCase)
        || string.Equals(segment, "delegate", StringComparison.OrdinalIgnoreCase)
        || string.Equals(segment, "lifecycle", StringComparison.OrdinalIgnoreCase);

    public static bool IsToolHostUpstreamRequest(PathString path)
    {
        var value = path.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 4
            && string.Equals(segments[0], "tool-host", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(segments[1])
            && string.Equals(segments[2], "api", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[3], "upstream", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return segments.Length >= 6
            && string.Equals(segments[0], "tool-host", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(segments[1])
            && string.Equals(segments[2], "api", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[3], "delegate", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(segments[4])
            && string.Equals(segments[5], "upstream", StringComparison.OrdinalIgnoreCase);
    }
}
