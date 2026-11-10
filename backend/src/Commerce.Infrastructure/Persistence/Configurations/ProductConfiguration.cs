using Commerce.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Name).HasMaxLength(PublishRules.NameMaxLength).IsRequired();
        builder.Property(product => product.Slug).HasMaxLength(Slug.MaxLength).IsRequired();
        builder.Property(product => product.ShortDescription).HasMaxLength(PublishRules.ShortDescriptionMaxLength).IsRequired();
        builder.Property(product => product.Description).HasMaxLength(PublishRules.DescriptionMaxLength).IsRequired();
        builder.Property(product => product.Brand).HasMaxLength(PublishRules.BrandMaxLength);
        builder.Property(product => product.Status)
            .HasConversion(
                status => status.ToString().ToLowerInvariant(),
                value => Enum.Parse<ProductStatus>(value, true))
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(product => product.Version).IsRequired().HasDefaultValue(1);
        builder.HasIndex(product => product.Slug).IsUnique();
        builder.HasIndex(product => new { product.Status, product.PublishedAt });

        builder.Property<NpgsqlTsVector>("SearchVector")
            .HasColumnType("tsvector")
            .HasComputedColumnSql(
                "to_tsvector('turkish', coalesce(name, '') || ' ' || coalesce(brand, '') || ' ' || coalesce(short_description, '') || ' ' || coalesce(description, ''))",
                stored: true);
        builder.HasIndex("SearchVector").HasMethod("GIN");
    }
}
