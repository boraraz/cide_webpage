using Commerce.Domain.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class ContentPageConfiguration : IEntityTypeConfiguration<ContentPage>
{
    public void Configure(EntityTypeBuilder<ContentPage> builder)
    {
        builder.HasKey(page => page.Id);
        builder.Property(page => page.Key).HasMaxLength(32).IsRequired();
        builder.HasIndex(page => page.Key).IsUnique();
        builder.Property(page => page.DraftVersion).IsRequired().HasDefaultValue(0);
        builder.HasOne(page => page.DraftRevision)
            .WithMany()
            .HasForeignKey(page => page.DraftRevisionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(page => page.PublishedRevision)
            .WithMany()
            .HasForeignKey(page => page.PublishedRevisionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ContentRevisionConfiguration : IEntityTypeConfiguration<ContentRevision>
{
    public void Configure(EntityTypeBuilder<ContentRevision> builder)
    {
        builder.HasKey(revision => revision.Id);
        builder.Property(revision => revision.Configuration).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(revision => new { revision.PageId, revision.Version }).IsUnique();
        builder.HasOne(revision => revision.Page)
            .WithMany()
            .HasForeignKey(revision => revision.PageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ContentMediaConfiguration : IEntityTypeConfiguration<ContentMedia>
{
    public void Configure(EntityTypeBuilder<ContentMedia> builder)
    {
        builder.HasKey(media => media.Id);
        builder.Property(media => media.ObjectKey).HasMaxLength(240).IsRequired();
        builder.Property(media => media.ContentType).HasMaxLength(32).IsRequired();
        builder.Property(media => media.AltText).HasMaxLength(300).IsRequired();
        builder.HasIndex(media => media.ObjectKey).IsUnique();
    }
}

internal sealed class ContentRevisionMediaConfiguration : IEntityTypeConfiguration<ContentRevisionMedia>
{
    public void Configure(EntityTypeBuilder<ContentRevisionMedia> builder)
    {
        builder.HasKey(link => new { link.RevisionId, link.MediaId });
        builder.HasOne(link => link.Revision)
            .WithMany(revision => revision.Media)
            .HasForeignKey(link => link.RevisionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(link => link.Media)
            .WithMany()
            .HasForeignKey(link => link.MediaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
