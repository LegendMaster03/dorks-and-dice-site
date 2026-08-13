using dorks_and_dice_site.Models.Content;

namespace dorks_and_dice_site.Services.Content;

public interface IContentAuthoringService
{
    Task<ContentAuthoringIndexViewModel> GetIndexAsync(CancellationToken cancellationToken = default);
    Task<ContentAuthoringEditViewModel?> GetEditAsync(string slug, CancellationToken cancellationToken = default);
    ContentAuthoringEditViewModel GetNew();
    Task<ContentItem> CreateAsync(ContentAuthoringDocument document, CancellationToken cancellationToken = default);
    Task<ContentItem> SaveRevisionAsync(ContentAuthoringDocument document, CancellationToken cancellationToken = default);
}

public sealed class ContentAuthoringConflictException : InvalidOperationException
{
    public ContentAuthoringConflictException(string message)
        : base(message)
    {
    }
}
