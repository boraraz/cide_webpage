using Commerce.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class ProductOptionConfiguration : IEntityTypeConfiguration<ProductOption>
{
    public void Configure(EntityTypeBuilder<ProductOption> builder)
    {
        builder.HasKey(option => option.Id);
        builder.Property(option => option.Name).HasMaxLength(PublishRules.OptionNameMaxLength).IsRequired();
        builder.Property(option => option.NormalizedName).HasMaxLength(Slug.MaxLength).IsRequired();
        builder.HasIndex(option => new { option.ProductId, option.NormalizedName }).IsUnique();
        builder.HasOne(option => option.Product)
            .WithMany(product => product.Options)
            .HasForeignKey(option => option.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
