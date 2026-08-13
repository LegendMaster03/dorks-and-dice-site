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
            entity.Property(asset => asset.PageId).HasColumnName("asset_page_id");
            entity.Property(asset => asset.FileName).HasColumnName("asset_file_name").IsRequired();
            entity.Property(asset => asset.MediaType).HasColumnName("asset_media_type").IsRequired();
            entity.Property(asset => asset.Length).HasColumnName("asset_length").IsRequired();
            entity.Property(asset => asset.Sha256).HasColumnName("asset_sha256").IsRequired();
            entity.Property(asset => asset.CreatedUtc).HasColumnName("asset_created_utc").IsRequired();
            entity.Property(asset => asset.Data).HasColumnName("asset_data").IsRequired();
            entity.HasOne(asset => asset.Page)
                .WithMany(page => page.Assets)
                .HasForeignKey(asset => asset.PageId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(asset => asset.AssetKey).IsUnique();
            entity.HasIndex(asset => new { asset.PageId, asset.Sha256 }).IsUnique();
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
    public List<ContentAssetRecord> Assets { get; set; } = [];
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
    public long PageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public long Length { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public byte[] Data { get; set; } = [];
    public ContentPageRecord? Page { get; set; }
}
