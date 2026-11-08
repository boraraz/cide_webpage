using Commerce.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class ProductMediaConfiguration : IEntityTypeConfiguration<ProductMedia>
{
    public void Configure(EntityTypeBuilder<ProductMedia> builder)
    {
        builder.HasKey(media => media.Id);
        builder.Property(media => media.ObjectKey).HasMaxLength(300).IsRequired();
        builder.Property(media => media.AltText).HasMaxLength(PublishRules.AltTextMaxLength).IsRequired();
        builder.Property(media => media.ContentType).HasMaxLength(64).IsRequired();
        builder.HasIndex(media => media.ObjectKey).IsUnique();
        builder.HasIndex(media => new { media.ProductId, media.DisplayOrder });
        builder.HasIndex(media => media.ProductId).IsUnique().HasFilter("is_primary");
        builder.HasOne(media => media.Product)
            .WithMany(product => product.Media)
            .HasForeignKey(media => media.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(media => media.Variant)
            .WithMany()
            .HasForeignKey(media => media.VariantId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
