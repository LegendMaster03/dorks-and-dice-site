using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Services.Content.Storage;

public sealed class ContentDbContext : DbContext
{
    public ContentDbContext(DbContextOptions<ContentDbContext> options)
        : base(options)
    {
    }

    internal DbSet<ContentPageRecord> Pages => Set<ContentPageRecord>();
    internal DbSet<ContentRevisionRecord> Revisions => Set<ContentRevisionRecord>();
    internal DbSet<ContentRevisionTagRecord> RevisionTags => Set<ContentRevisionTagRecord>();
    internal DbSet<ContentRevisionModeRecord> RevisionModes => Set<ContentRevisionModeRecord>();
    internal DbSet<ContentAssetRecord> Assets => Set<ContentAssetRecord>();
    internal DbSet<ContentPageAssetRecord> PageAssets => Set<ContentPageAssetRecord>();
    internal DbSet<ContentRevisionAssetRecord> RevisionAssets => Set<ContentRevisionAssetRecord>();
    internal DbSet<ContentPageAssetDependencyRecord> PageAssetDependencies => Set<ContentPageAssetDependencyRecord>();
    internal DbSet<ContentRedirectRecord> Redirects => Set<ContentRedirectRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ContentPageRecord>(entity =>
        {
            entity.ToTable("content_page");
            entity.HasKey(page => page.Id);
            entity.Property(page => page.Id).HasColumnName("page_id");
            entity.Property(page => page.ContentKey).HasColumnName("page_key").IsRequired();
            entity.Property(page => page.Slug).HasColumnName("page_slug").IsRequired();
            entity.Property(page => page.CurrentRevisionId).HasColumnName("page_current_revision_id");
            entity.HasIndex(page => page.ContentKey).IsUnique();
            entity.HasIndex(page => page.Slug).IsUnique();
            entity.HasOne(page => page.CurrentRevision)
                .WithMany()
                .HasForeignKey(page => page.CurrentRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ContentRevisionRecord>(entity =>
        {
            entity.ToTable("content_revision");
            entity.HasKey(revision => revision.Id);
            entity.Property(revision => revision.Id).HasColumnName("revision_id");
            entity.Property(revision => revision.PageId).HasColumnName("revision_page_id");
            entity.Property(revision => revision.ParentRevisionId).HasColumnName("revision_parent_id");
            entity.Property(revision => revision.CreatedUtc).HasColumnName("revision_created_utc").IsRequired();
            entity.Property(revision => revision.BodyFormat).HasColumnName("revision_body_format").IsRequired();
            entity.Property(revision => revision.MetadataJson).HasColumnName("revision_metadata_json").IsRequired();
            entity.Property(revision => revision.Body).HasColumnName("revision_body").IsRequired();
            entity.HasOne(revision => revision.Page)
                .WithMany(page => page.Revisions)
                .HasForeignKey(revision => revision.PageId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(revision => revision.ParentRevision)
                .WithMany()
                .HasForeignKey(revision => revision.ParentRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(revision => new { revision.PageId, revision.CreatedUtc });
        });

        modelBuilder.Entity<ContentRevisionTagRecord>(entity =>
        {
            entity.ToTable("content_revision_tag");
            entity.HasKey(tag => new { tag.RevisionId, tag.Tag });
            entity.Property(tag => tag.RevisionId).HasColumnName("revision_id");
            entity.Property(tag => tag.Tag).HasColumnName("tag").IsRequired();
            entity.HasOne(tag => tag.Revision)
                .WithMany(revision => revision.Tags)
                .HasForeignKey(tag => tag.RevisionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(tag => tag.Tag);
        });

        modelBuilder.Entity<ContentRevisionModeRecord>(entity =>
        {
            entity.ToTable("content_revision_mode");
            entity.HasKey(mode => new { mode.RevisionId, mode.SiteMode });
            entity.Property(mode => mode.RevisionId).HasColumnName("revision_id");
            entity.Property(mode => mode.SiteMode).HasColumnName("site_mode").IsRequired();
            entity.HasOne(mode => mode.Revision)
                .WithMany(revision => revision.Modes)
                .HasForeignKey(mode => mode.RevisionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(mode => mode.SiteMode);
        });

        modelBuilder.Entity<ContentAssetRecord>(entity =>
        {
            entity.ToTable("content_asset");
            entity.HasKey(asset => asset.Id);
            entity.Property(asset => asset.Id).HasColumnName("asset_id");
            entity.Property(asset => asset.AssetKey).HasColumnName("asset_key").IsRequired();
            entity.Property(asset => asset.FileName).HasColumnName("asset_file_name").IsRequired();
            entity.Property(asset => asset.MediaType).HasColumnName("asset_media_type").IsRequired();
            entity.Property(asset => asset.Length).HasColumnName("asset_length").IsRequired();
            entity.Property(asset => asset.Sha256).HasColumnName("asset_sha256").IsRequired();
            entity.Property(asset => asset.CreatedUtc).HasColumnName("asset_created_utc").IsRequired();
            entity.Property(asset => asset.Data).HasColumnName("asset_data").IsRequired();
            entity.HasIndex(asset => asset.AssetKey).IsUnique();
            entity.HasIndex(asset => asset.Sha256);
        });

        modelBuilder.Entity<ContentPageAssetRecord>(entity =>
        {
            entity.ToTable("content_page_asset");
            entity.HasKey(link => new { link.PageId, link.AssetId });
            entity.Property(link => link.PageId).HasColumnName("page_id");
            entity.Property(link => link.AssetId).HasColumnName("asset_id");
            entity.Property(link => link.Relationship).HasColumnName("relationship").IsRequired();
            entity.HasOne(link => link.Page)
                .WithMany(page => page.AssetLinks)
                .HasForeignKey(link => link.PageId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(link => link.Asset)
                .WithMany(asset => asset.PageLinks)
                .HasForeignKey(link => link.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(link => link.AssetId);
        });

        modelBuilder.Entity<ContentRevisionAssetRecord>(entity =>
        {
            entity.ToTable("content_revision_asset");
            entity.HasKey(link => new { link.RevisionId, link.AssetKey });
            entity.Property(link => link.RevisionId).HasColumnName("revision_id");
            entity.Property(link => link.AssetKey).HasColumnName("asset_key").IsRequired();
            entity.Property(link => link.Relationship).HasColumnName("relationship").IsRequired();
            entity.HasOne(link => link.Revision)
                .WithMany(revision => revision.AssetReferences)
                .HasForeignKey(link => link.RevisionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(link => link.AssetKey);
        });

        modelBuilder.Entity<ContentPageAssetDependencyRecord>(entity =>
        {
            entity.ToTable("content_page_asset_dependency");
            entity.HasKey(link => new { link.PageId, link.AssetSourceKey, link.AssetKey });
            entity.Property(link => link.PageId).HasColumnName("page_id");
            entity.Property(link => link.AssetSourceKey).HasColumnName("asset_source_key").IsRequired();
            entity.Property(link => link.AssetKey).HasColumnName("asset_key").IsRequired();
            entity.HasOne(link => link.Page).WithMany(page => page.AssetDependencies)
                .HasForeignKey(link => link.PageId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(link => link.AssetKey);
        });

        modelBuilder.Entity<ContentRedirectRecord>(entity =>
        {
            entity.ToTable("content_redirect");
            entity.HasKey(redirect => redirect.Id);
            entity.Property(redirect => redirect.Id).HasColumnName("redirect_id");
            entity.Property(redirect => redirect.Namespace).HasColumnName("redirect_namespace").IsRequired();
            entity.Property(redirect => redirect.Slug).HasColumnName("redirect_slug").IsRequired();
            entity.Property(redirect => redirect.PageId).HasColumnName("redirect_page_id");
            entity.Property(redirect => redirect.CreatedUtc).HasColumnName("redirect_created_utc").IsRequired();
            entity.HasOne(redirect => redirect.Page)
                .WithMany(page => page.Redirects)
                .HasForeignKey(redirect => redirect.PageId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(redirect => new { redirect.Namespace, redirect.Slug }).IsUnique();
            entity.HasIndex(redirect => redirect.PageId);
        });
    }
}

internal sealed class ContentPageRecord
{
    public long Id { get; set; }
    public string ContentKey { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public long? CurrentRevisionId { get; set; }
    public ContentRevisionRecord? CurrentRevision { get; set; }
    public List<ContentRevisionRecord> Revisions { get; set; } = [];
    public List<ContentPageAssetRecord> AssetLinks { get; set; } = [];
    public List<ContentPageAssetDependencyRecord> AssetDependencies { get; set; } = [];
    public List<ContentRedirectRecord> Redirects { get; set; } = [];
}

internal sealed class ContentRevisionRecord
{
    public long Id { get; set; }
    public long PageId { get; set; }
    public long? ParentRevisionId { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string BodyFormat { get; set; } = "markdown";
    public string MetadataJson { get; set; } = "{}";
    public string Body { get; set; } = string.Empty;
    public ContentPageRecord? Page { get; set; }
    public ContentRevisionRecord? ParentRevision { get; set; }
    public List<ContentRevisionTagRecord> Tags { get; set; } = [];
    public List<ContentRevisionModeRecord> Modes { get; set; } = [];
    public List<ContentRevisionAssetRecord> AssetReferences { get; set; } = [];
}

internal sealed class ContentRevisionTagRecord
{
    public long RevisionId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public ContentRevisionRecord? Revision { get; set; }
}

internal sealed class ContentRevisionModeRecord
{
    public long RevisionId { get; set; }
    public string SiteMode { get; set; } = string.Empty;
    public ContentRevisionRecord? Revision { get; set; }
}

internal sealed class ContentAssetRecord
{
    public long Id { get; set; }
    public string AssetKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public long Length { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public byte[] Data { get; set; } = [];
    public List<ContentPageAssetRecord> PageLinks { get; set; } = [];
}

internal sealed class ContentPageAssetRecord
{
    public long PageId { get; set; }
    public long AssetId { get; set; }
    public string Relationship { get; set; } = ContentAssetRelationships.Owned;
    public ContentPageRecord? Page { get; set; }
    public ContentAssetRecord? Asset { get; set; }
}

internal sealed class ContentRevisionAssetRecord
{
    public long RevisionId { get; set; }
    public string AssetKey { get; set; } = string.Empty;
    public string Relationship { get; set; } = ContentAssetRelationships.Embedded;
    public ContentRevisionRecord? Revision { get; set; }
}

internal sealed class ContentPageAssetDependencyRecord
{
    public long PageId { get; set; }
    public string AssetSourceKey { get; set; } = string.Empty;
    public string AssetKey { get; set; } = string.Empty;
    public ContentPageRecord? Page { get; set; }
}

internal sealed class ContentRedirectRecord
{
    public long Id { get; set; }
    public string Namespace { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public long PageId { get; set; }
    public DateTime CreatedUtc { get; set; }
    public ContentPageRecord? Page { get; set; }
}

internal static class ContentAssetRelationships
{
    public const string Owned = "owned";
    public const string Attached = "attached";
    public const string Embedded = "embedded";
}
