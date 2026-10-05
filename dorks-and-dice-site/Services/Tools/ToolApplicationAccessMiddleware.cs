using dorks_and_dice_site.Models.Tools;
using dorks_and_dice_site.Services.Site;
using Microsoft.AspNetCore.Authentication;

namespace dorks_and_dice_site.Services.Tools;

/// <summary>
/// Enforces the authoritative user-facing application access policy before any Tool page,
/// bootstrap resource, hosted API, module asset, or proxied application request reaches its
/// owning controller or upstream. Backend capability routes use their separate ticket/capability
/// authorization model and are deliberately excluded.
/// </summary>
public sealed class ToolApplicationAccessMiddleware(
    RequestDelegate next,
    IToolRegistry toolRegistry)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!TryGetUserFacingApplicationSlug(context.Request.Path, out var slug))
        {
            await next(context);
            return;
        }

        var tool = await toolRegistry.GetBySlugAsync(slug, context.RequestAborted);
        var modeId = context.GetSiteModeContext().ActiveModeId;
        var decision = ToolApplicationAccessPolicy.Evaluate(tool, modeId, context.User);

        // The response varies by account/release authorization even when access is denied. Apply
        // cache isolation before branching so a shared cache can not preserve a restrictive 404
        // or authentication challenge and later serve it to an authorized user.
        if (tool is not null && ToolApplicationAccessPolicy.RequiresPrivateNoStore(tool))
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.CacheControl = "private, no-store";
                return Task.CompletedTask;
            });
        }

        switch (decision)
        {
            case ToolApplicationAccessDecision.Allowed:
                await next(context);
                return;

            case ToolApplicationAccessDecision.AuthenticationRequired:
                await context.ChallengeAsync();
                return;

            case ToolApplicationAccessDecision.ReleaseAudienceDenied:
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;

            case ToolApplicationAccessDecision.Unavailable:
            default:
                // Structural availability remains the owning controller's responsibility. This
                // preserves existing 404/problem behavior for missing, disabled, wrong-mode, or
                // malformed registrations while keeping authorization semantics centralized.
                await next(context);
                return;
        }
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
        || string.Equals(segment, "lifecycle", StringComparison.OrdinalIgnoreCase)
        || string.Equals(segment, "private-tunnel", StringComparison.OrdinalIgnoreCase);
}
