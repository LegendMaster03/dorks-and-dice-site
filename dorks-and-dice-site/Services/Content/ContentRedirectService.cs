using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Services.Content;

public interface IContentRedirectService
{
    Task<ContentRedirectTarget?> ResolveAsync(
        string routeNamespace,
        string slug,
        CancellationToken cancellationToken = default);
}

public sealed class ContentRedirectService : IContentRedirectService
{
    private readonly IContentSourceRegistry _sourceRegistry;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ContentRedirectService(
        IContentSourceRegistry sourceRegistry,
        IHttpContextAccessor httpContextAccessor)
    {
        _sourceRegistry = sourceRegistry;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ContentRedirectTarget?> ResolveAsync(
        string routeNamespace,
        string slug,
        CancellationToken cancellationToken = default)
    {
        if (!ContentRouteNamespaces.IsKnown(routeNamespace))
        {
            throw new InvalidOperationException($"Unknown content route namespace '{routeNamespace}'.");
        }

        var normalizedSlug = slug.ToLowerInvariant();
        try
        {
            ContentInputValidator.ValidateKey("Redirect slug", normalizedSlug);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        var modeContext = GetSiteModeContext();
        var sources = _sourceRegistry.GetSourcesForContext(modeContext);

        for (var index = sources.Count - 1; index >= 0; index--)
        {
            var options = new DbContextOptionsBuilder<ContentDbContext>();
            _sourceRegistry.ConfigureDbContext(options, sources[index].Key);
            await using var context = new ContentDbContext(options.Options);
            var contentKey = await context.Redirects
                .AsNoTracking()
                .Where(redirect => redirect.Namespace == routeNamespace && redirect.Slug == normalizedSlug)
                .Select(redirect => redirect.Page!.ContentKey)
                .SingleOrDefaultAsync(cancellationToken);

            if (contentKey is not null)
            {
                return new ContentRedirectTarget { ContentKey = contentKey };
            }
        }

        return null;
    }

    private SiteModeContext GetSiteModeContext()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.Items[SiteModeContext.HttpContextItemKey] is SiteModeContext siteModeContext)
        {
            return siteModeContext;
        }

        return new SiteModeContext
        {
            FrameworkState = FrameworkRuntimeStates.TrustedPreview,
            IsDevelopmentPreview = true
        };
    }
}
