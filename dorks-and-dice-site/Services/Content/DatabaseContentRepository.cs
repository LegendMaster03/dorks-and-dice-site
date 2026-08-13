using dorks_and_dice_site.Models.Content;
using dorks_and_dice_site.Services.Content.Storage;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Services.Content;

public sealed class DatabaseContentRepository : IContentRepository
{
    private readonly ContentDbContext _context;

    public DatabaseContentRepository(ContentDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ContentItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var pages = await _context.Pages
            .AsNoTracking()
            .Include(page => page.CurrentRevision)!
                .ThenInclude(revision => revision!.Tags)
            .Include(page => page.CurrentRevision)!
                .ThenInclude(revision => revision!.Modes)
            .Where(page => page.CurrentRevisionId != null)
            .OrderBy(page => page.Id)
            .ToListAsync(cancellationToken);

        return pages.Select(ToContentItem).ToList();
    }

    public async Task<ContentItem?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var page = await _context.Pages
            .AsNoTracking()
            .Include(candidate => candidate.CurrentRevision)!
                .ThenInclude(revision => revision!.Tags)
            .Include(candidate => candidate.CurrentRevision)!
                .ThenInclude(revision => revision!.Modes)
            .SingleOrDefaultAsync(
                candidate => candidate.Slug == slug && candidate.CurrentRevisionId != null,
                cancellationToken);

        return page is null ? null : ToContentItem(page);
    }

    private static ContentItem ToContentItem(ContentPageRecord page)
    {
        var revision = page.CurrentRevision
            ?? throw new InvalidOperationException($"Content page '{page.ContentKey}' has no current revision.");
        return ContentRecordMapper.ToContentItem(page, revision);
    }
}
