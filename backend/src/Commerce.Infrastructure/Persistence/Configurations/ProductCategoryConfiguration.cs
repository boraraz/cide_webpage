using Commerce.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.HasKey(link => new { link.ProductId, link.CategoryId });
        builder.HasOne(link => link.Product)
            .WithMany(product => product.Categories)
            .HasForeignKey(link => link.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(link => link.Category)
            .WithMany(category => category.ProductCategories)
            .HasForeignKey(link => link.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
