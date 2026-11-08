using Commerce.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class ProductCollectionConfiguration : IEntityTypeConfiguration<ProductCollection>
{
    public void Configure(EntityTypeBuilder<ProductCollection> builder)
    {
        builder.HasKey(link => new { link.ProductId, link.CollectionId });
        builder.HasOne(link => link.Product)
            .WithMany(product => product.Collections)
            .HasForeignKey(link => link.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(link => link.Collection)
            .WithMany(collection => collection.ProductCollections)
            .HasForeignKey(link => link.CollectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
