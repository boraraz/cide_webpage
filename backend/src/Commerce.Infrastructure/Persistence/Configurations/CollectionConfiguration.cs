using Commerce.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> builder)
    {
        builder.HasKey(collection => collection.Id);
        builder.Property(collection => collection.Name).HasMaxLength(PublishRules.CategoryNameMaxLength).IsRequired();
        builder.Property(collection => collection.Slug).HasMaxLength(Slug.MaxLength).IsRequired();
        builder.Property(collection => collection.Description).HasMaxLength(PublishRules.DescriptionOptionalMaxLength);
        builder.HasIndex(collection => collection.Slug).IsUnique();
        builder.HasIndex(collection => new { collection.IsActive, collection.DisplayOrder });
    }
}
