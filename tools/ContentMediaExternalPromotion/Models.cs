using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ContentMediaExternalPromotion;

public sealed class PromotionException(string message) : Exception(message);
public sealed record ManifestEntry(string Slug, string OldUrl, string LocalAssetKey);
public sealed class Row() : SortedDictionary<string, string?>(StringComparer.Ordinal)
{
    public string Text(string name) => this[name] ?? throw new PromotionException($"Missing required field {name}.");
    public long Number(string name) => long.Parse(Text(name), System.Globalization.CultureInfo.InvariantCulture);
}
public sealed class Snapshot
{
    public Dictionary<string, List<Row>> Tables { get; set; } = [];
    public List<Row> Rows(string table) => Tables[table];
    public Row? Page(string slug) => Rows("content_page").SingleOrDefault(r => r.Text("page_slug") == slug);
    public Row Current(Row page) => Rows("content_revision").Single(r => r["revision_id"] == page["page_current_revision_id"]);
    public Row? Asset(string key) => Rows("content_asset").SingleOrDefault(r => r.Text("asset_key") == key);
    public bool Attached(Row page, string key) => Asset(key) is { } asset && Rows("content_page_asset").Any(r => r["page_id"] == page["page_id"] && r["asset_id"] == asset["asset_id"]);
    public List<Row> Related(string table, string field, string? id) => Rows(table).Where(r => r[field] == id).ToList();
    public string Fingerprint => StateStore.Hash(Tables);
    public PageBaseline Baseline(Row page)
    {
        var revisions = Related("content_revision", "revision_page_id", page["page_id"]);
        var ids = revisions.Select(r => r["revision_id"]).ToHashSet();
        return new PageBaseline
        {
            Page = page, Current = Current(page),
            History = new() { ["content_revision"] = revisions,
                ["content_revision_tag"] = Rows("content_revision_tag").Where(r => ids.Contains(r["revision_id"])).ToList(),
                ["content_revision_mode"] = Rows("content_revision_mode").Where(r => ids.Contains(r["revision_id"])).ToList(),
                ["content_revision_asset"] = Rows("content_revision_asset").Where(r => ids.Contains(r["revision_id"])).ToList() },
            Redirects = Related("content_redirect", "redirect_page_id", page["page_id"]),
            Attachments = Related("content_page_asset", "page_id", page["page_id"]),
            GlobalDependencies = Related("content_page_asset_dependency", "page_id", page["page_id"])
        };
    }
}
public sealed class PageBaseline
{
    public Row Page { get; set; } = new();
    public Row Current { get; set; } = new();
    public Dictionary<string, List<Row>> History { get; set; } = [];
    public List<Row> Redirects { get; set; } = [];
    public List<Row> Attachments { get; set; } = [];
    public List<Row> GlobalDependencies { get; set; } = [];
}
public sealed class PlanEntry
{
    public required ManifestEntry Manifest { get; set; }
    public string Sha256 { get; set; } = "";
    public string FileName { get; set; } = "";
    public string MediaType { get; set; } = "";
    public bool MissingPage { get; set; }
    public bool Applicable { get; set; }
    public bool BodyReference { get; set; }
    public bool MetadataReference { get; set; }
    public bool ExistingDependency { get; set; }
    public string? ExistingExternalKey { get; set; }
    public string? ProposedExternalUrl { get; set; }
    public bool Safe { get; set; }
    public string Reason { get; set; } = "";
}
public sealed class PromotionPlan
{
    public int Version { get; set; } = 1;
    public Guid OperationId { get; set; } = Guid.NewGuid();
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string DatabaseIdentity { get; set; } = "";
    public string ManifestHash { get; set; } = "";
    public string LocalFingerprint { get; set; } = "";
    public string BackupFingerprint { get; set; } = "";
    public List<PlanEntry> Entries { get; set; } = [];
    public Dictionary<string, PageBaseline> Pages { get; set; } = [];
}
public sealed class StagedAsset
{
    public string LocalKey { get; set; } = "";
    public string ExternalKey { get; set; } = "";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public bool Created { get; set; }
    public DateTime RecordedUtc { get; set; } = DateTime.UtcNow;
}
public sealed record AddedDependency(string Slug, string AssetKey, string? Version = null);
public sealed class RevisionChange
{
    public long PreviousId { get; set; }
    public long? CreatedId { get; set; }
    public string ExpectedBody { get; set; } = "";
    public string ExpectedMetadata { get; set; } = "";
    public string? CreatedChecksum { get; set; }
    public string Status { get; set; } = "prepared";
}
public sealed class Journal
{
    public Guid OperationId { get; set; }
    public string PlanHash { get; set; } = "";
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, StagedAsset> Assets { get; set; } = [];
    public List<AddedDependency> AddedDependencies { get; set; } = [];
    public Dictionary<string, RevisionChange> Revisions { get; set; } = [];
    public bool StagingValidated { get; set; }
    public string Status { get; set; } = "staging";
}
public static class StateStore
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static string Hash<T>(T value) => Sha(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json)));
    public static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public static void Save<T>(string path, T value)
    {
        var envelope = JsonSerializer.SerializeToUtf8Bytes(new Envelope<T>(Hash(value), value), Json);
        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(envelope); stream.Flush(true); }
        File.Move(temp, path, true);
    }
    public static T Load<T>(string path)
    {
        var item = JsonSerializer.Deserialize<Envelope<T>>(File.ReadAllBytes(path), Json) ?? throw new PromotionException("Invalid state file.");
        if (Hash(item.Value) != item.Sha256) throw new PromotionException("State checksum mismatch; refusing writes.");
        return item.Value;
    }
    private sealed record Envelope<T>(string Sha256, T Value);
    public static string Url(Row asset) => $"/content/media/{asset.Text("asset_key")}/{asset.Text("asset_file_name")}";
}
public static class References
{
    private static Regex Pattern(string url)
    {
        // Exact, bounded manifest paths only. Support ~/, raw spaces, and percent encoding without touching unrelated URLs.
        var pattern = string.Concat(url.Select(c => c == '/' ? "/" : $"(?:{Regex.Escape(c.ToString())}|%{(int)c:X2})"));
        return new Regex(@"(?<![A-Za-z0-9_/%:.-])~?" + pattern + "(?=$|[\\s)\\\"'?#<>{}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
    public static bool Contains(string text, string url) => Pattern(url).IsMatch(text);
    public static int Count(string text, string url) => Pattern(url).Matches(text).Count;
    public static string Replace(string text, string oldUrl, string newUrl) => Pattern(oldUrl).Replace(text, _ => newUrl);
    public static string Metadata(string json, Func<string, string> transform)
    {
        var node = JsonNode.Parse(json) ?? throw new PromotionException("Invalid metadata.");
        void Walk(JsonNode current)
        {
            if (current is JsonObject obj) foreach (var key in obj.Select(p => p.Key).ToArray())
            { if (obj[key] is JsonValue val && val.TryGetValue<string>(out var s)) obj[key] = transform(s); else if (obj[key] is { } child) Walk(child); }
            else if (current is JsonArray array) for (int i = 0; i < array.Count; i++)
            { if (array[i] is JsonValue val && val.TryGetValue<string>(out var s)) array[i] = transform(s); else if (array[i] is { } child) Walk(child); }
        }
        Walk(node); return node.ToJsonString();
    }
    public static bool InMetadata(string json, string url)
    { bool found = false; Metadata(json, s => { found |= Contains(s, url); return s; }); return found; }
    public static bool SameMetadata(string left, string right) => JsonNode.DeepEquals(JsonNode.Parse(left), JsonNode.Parse(right));
}
