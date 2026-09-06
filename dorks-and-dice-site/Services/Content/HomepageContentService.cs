using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content.Storage;
using dorks_and_dice_site.Services.Site;
using Microsoft.Extensions.Logging.Abstractions;

namespace dorks_and_dice_site.Services.Content;

public interface IHomepageContentService
{
    Task<HomepageContentViewModel?> GetAsync(
        SiteModeContext modeContext,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves the database-backed homepage document visible to a normal mode and composes
/// its authored Markdown with installed page components. Mode definition storage is deliberately
/// outside this service so the same page contract works whether normal modes ultimately remain
/// compiled definitions or become runtime data.
/// </summary>
public sealed class HomepageContentService : IHomepageContentService
{
    private readonly IContentCatalogService _catalog;
    private readonly IContentPageComposer _pageComposer;
    private readonly IContentSourceRegistry? _sourceRegistry;
    private readonly ILogger<HomepageContentService> _logger;

    // Compatibility constructor for focused unit fixtures. Runtime DI uses the full constructor
    // so source precedence participates in duplicate recovery.
    public HomepageContentService(
        IContentCatalogService catalog,
        IContentPageComposer pageComposer)
        : this(catalog, pageComposer, null, NullLogger<HomepageContentService>.Instance)
    {
    }

    public HomepageContentService(
        IContentCatalogService catalog,
        IContentPageComposer pageComposer,
        IContentSourceRegistry? sourceRegistry,
        ILogger<HomepageContentService> logger)
    {
        _catalog = catalog;
        _pageComposer = pageComposer;
        _sourceRegistry = sourceRegistry;
        _logger = logger;
    }

    public async Task<HomepageContentViewModel?> GetAsync(
        SiteModeContext modeContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(modeContext);

        if (modeContext.ActiveModeId is not { Length: > 0 } activeModeId)
        {
            return null;
        }

        var catalogCandidates = await _catalog.GetByContextAsync(
            ContentTags.Homepage,
            modeContext,
            includeUnlisted: true,
            cancellationToken);

        // Synthetic Development intentionally allows a Dev to inspect content across normal modes.
        // Homepage composition is different: the selected normal ribbon mode owns the '/' surface,
        // so cross-mode inspection authority must not cause other modes' homepage documents to
        // participate in that singleton selection.
        var candidates = catalogCandidates
            .Where(candidate => candidate.IsVisibleInMode(activeModeId))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var item = ResolveCandidate(candidates, modeContext);
        return new HomepageContentViewModel
        {
            Item = item,
            Fragments = _pageComposer.Compose(item.BodyFormat, item.Body)
        };
    }

    private ContentItem ResolveCandidate(
        IReadOnlyList<ContentItem> candidates,
        SiteModeContext modeContext)
    {
        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        IEnumerable<ContentItem> preferred = candidates;

        // Content sources are composed base-to-override. If duplicate singleton homepage roles
        // exist across different sources, a homepage in the highest-precedence selected source wins.
        if (_sourceRegistry is not null)
        {
            var sourceRanks = _sourceRegistry
                .GetSourcesForContext(modeContext)
                .Select((source, index) => new { source.Key, Index = index })
                .ToDictionary(pair => pair.Key, pair => pair.Index, StringComparer.OrdinalIgnoreCase);

            var rankedCandidates = candidates
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate.SourceKey)
                    && sourceRanks.ContainsKey(candidate.SourceKey))
                .ToList();
            if (rankedCandidates.Count > 0)
            {
                var highestRank = rankedCandidates.Max(candidate => sourceRanks[candidate.SourceKey]);
                preferred = rankedCandidates.Where(candidate => sourceRanks[candidate.SourceKey] == highestRank);
            }
        }

        var preferredList = preferred.ToList();
        var minimumModeCount = preferredList.Min(candidate => candidate.VisibleInModes.Count);
        preferredList = preferredList
            .Where(candidate => candidate.VisibleInModes.Count == minimumModeCount)
            .ToList();

        // Revision IDs are monotonic within one source. Remaining duplicates therefore resolve to
        // the most recently revised candidate, with stable ID as the final deterministic tie-breaker.
        var selected = preferredList
            .OrderByDescending(candidate => candidate.RevisionId)
            .ThenBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase)
            .First();

        _logger.LogWarning(
            "Site mode {ModeId} has multiple homepage documents after active-mode filtering. Selected {SelectedId} from source {SourceKey}; candidates: {CandidateIds}",
            modeContext.ActiveModeId,
            selected.Id,
            selected.SourceKey,
            string.Join(", ", candidates.Select(candidate =>
                $"{candidate.Id}@{candidate.SourceKey}#r{candidate.RevisionId}")));

        return selected;
    }
}
