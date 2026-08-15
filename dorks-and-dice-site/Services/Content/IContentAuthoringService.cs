using dorks_and_dice_site.Models.Content;

namespace dorks_and_dice_site.Services.Content;

public interface IContentAuthoringService
{
    string DefaultSourceKey { get; }
    Task<ContentAuthoringIndexViewModel> GetIndexAsync(string? sourceKey, CancellationToken cancellationToken = default);
    Task<ContentAuthoringEditViewModel?> GetEditAsync(string sourceKey, string slug, CancellationToken cancellationToken = default);
    ContentAuthoringEditViewModel GetNew(string? sourceKey);
    void PopulateOptions(ContentAuthoringEditViewModel model);
    Task<ContentItem> CreateAsync(ContentAuthoringDocument document, CancellationToken cancellationToken = default);
    Task<ContentItem> SaveRevisionAsync(ContentAuthoringDocument document, CancellationToken cancellationToken = default);
    Task MoveAsync(string sourceKey, string targetSourceKey, string slug, CancellationToken cancellationToken = default);
    Task<int> MoveAllAsync(string sourceKey, string targetSourceKey, CancellationToken cancellationToken = default);
}

public sealed class ContentAuthoringConflictException : InvalidOperationException
{
    public ContentAuthoringConflictException(string message)
        : base(message)
    {
    }
}
