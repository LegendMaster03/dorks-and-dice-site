using System.Text.Json;
using dorks_and_dice_site.Models.Campaigns;

namespace dorks_and_dice_site.Services.Campaigns;

public interface ICampaignAccessStore
{
    Task<IReadOnlyList<CampaignAccessSummary>> GetCampaignsForUserAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<CampaignAccessSummary?> GetCampaignForUserAsync(
        Guid campaignId,
        string userId,
        CancellationToken cancellationToken = default);

    Task SaveCampaignAsync(CampaignRecord campaign, CancellationToken cancellationToken = default);
    Task SaveMembershipAsync(CampaignMembershipRecord membership, CancellationToken cancellationToken = default);
    Task<bool> DeleteCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);
}

public sealed class JsonCampaignAccessStore : ICampaignAccessStore
{
    public const string StoragePathConfigurationKey = "CampaignStorage:Path";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly string _storagePath;

    public JsonCampaignAccessStore(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var configuredPath = configuration[StoragePathConfigurationKey] ?? "Content/campaign-access.json";
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException($"{StoragePathConfigurationKey} must not be empty.");
        }

        _storagePath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);
    }

    public async Task<IReadOnlyList<CampaignAccessSummary>> GetCampaignsForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        await Gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadUnsafeAsync(cancellationToken);
            return BuildSummaries(document, userId)
                .OrderBy(campaign => campaign.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<CampaignAccessSummary?> GetCampaignForUserAsync(
        Guid campaignId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        await Gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadUnsafeAsync(cancellationToken);
            return BuildSummaries(document, userId)
                .FirstOrDefault(campaign => campaign.Id == campaignId);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task SaveCampaignAsync(CampaignRecord campaign, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        if (campaign.Id == Guid.Empty)
        {
            throw new ArgumentException("Campaign ID must not be empty.", nameof(campaign));
        }
        if (string.IsNullOrWhiteSpace(campaign.Name))
        {
            throw new ArgumentException("Campaign name must not be empty.", nameof(campaign));
        }

        await Gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadUnsafeAsync(cancellationToken);
            var index = document.Campaigns.FindIndex(existing => existing.Id == campaign.Id);
            if (index >= 0)
            {
                document.Campaigns[index] = campaign;
            }
            else
            {
                document.Campaigns.Add(campaign);
            }

            await WriteUnsafeAsync(document, cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task SaveMembershipAsync(
        CampaignMembershipRecord membership,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(membership);
        if (membership.CampaignId == Guid.Empty)
        {
            throw new ArgumentException("Campaign ID must not be empty.", nameof(membership));
        }
        if (string.IsNullOrWhiteSpace(membership.UserId))
        {
            throw new ArgumentException("User ID must not be empty.", nameof(membership));
        }
        if (!CampaignRoles.All.Contains(membership.Role))
        {
            throw new ArgumentException($"Campaign role '{membership.Role}' is not supported.", nameof(membership));
        }

        await Gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadUnsafeAsync(cancellationToken);
            if (!document.Campaigns.Any(campaign => campaign.Id == membership.CampaignId))
            {
                throw new InvalidOperationException("Membership campaign does not exist.");
            }

            var index = document.Memberships.FindIndex(existing =>
                existing.CampaignId == membership.CampaignId
                && string.Equals(existing.UserId, membership.UserId, StringComparison.Ordinal));
            if (index >= 0)
            {
                document.Memberships[index] = membership;
            }
            else
            {
                document.Memberships.Add(membership);
            }

            await WriteUnsafeAsync(document, cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<bool> DeleteCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var document = await ReadUnsafeAsync(cancellationToken);
            var removed = document.Campaigns.RemoveAll(campaign => campaign.Id == campaignId) > 0;
            if (!removed)
            {
                return false;
            }

            document.Memberships.RemoveAll(membership => membership.CampaignId == campaignId);
            await WriteUnsafeAsync(document, cancellationToken);
            return true;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static IEnumerable<CampaignAccessSummary> BuildSummaries(
        CampaignAccessDocument document,
        string userId)
    {
        var campaigns = document.Campaigns
            .Where(campaign => campaign.Enabled)
            .ToDictionary(campaign => campaign.Id);

        foreach (var membership in document.Memberships.Where(membership =>
                     string.Equals(membership.UserId, userId, StringComparison.Ordinal)
                     && CampaignRoles.All.Contains(membership.Role)))
        {
            if (campaigns.TryGetValue(membership.CampaignId, out var campaign))
            {
                yield return new CampaignAccessSummary
                {
                    Id = campaign.Id,
                    Name = campaign.Name,
                    Role = membership.Role
                };
            }
        }
    }

    private async Task<CampaignAccessDocument> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_storagePath))
        {
            return new CampaignAccessDocument();
        }

        await using var stream = File.OpenRead(_storagePath);
        return await JsonSerializer.DeserializeAsync<CampaignAccessDocument>(stream, JsonOptions, cancellationToken)
            ?? new CampaignAccessDocument();
    }

    private async Task WriteUnsafeAsync(CampaignAccessDocument document, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{_storagePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
            }

            File.Move(temporaryPath, _storagePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
