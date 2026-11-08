using Commerce.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_product_variants_price_nonnegative", "price >= 0");
            table.HasCheckConstraint(
                "ck_product_variants_compare_at",
                "compare_at_price IS NULL OR compare_at_price > price");
            table.HasCheckConstraint("ck_product_variants_currency_try", "currency_code = 'TRY'");
        });
        builder.HasKey(variant => variant.Id);
        builder.Property(variant => variant.Sku).HasMaxLength(Sku.MaxLength).IsRequired();
        builder.Property(variant => variant.Barcode).HasMaxLength(Sku.MaxLength);
        builder.Property(variant => variant.Price).HasPrecision(12, 2);
        builder.Property(variant => variant.CompareAtPrice).HasPrecision(12, 2);
        builder.Property(variant => variant.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(variant => variant.CombinationKey).HasMaxLength(2000).IsRequired();
        builder.HasIndex(variant => variant.Sku).IsUnique();
        builder.HasIndex(variant => variant.Barcode).IsUnique().HasFilter("barcode IS NOT NULL");
        builder.HasIndex(variant => new { variant.ProductId, variant.CombinationKey }).IsUnique();
        builder.HasOne(variant => variant.Product)
            .WithMany(product => product.Variants)
            .HasForeignKey(variant => variant.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
