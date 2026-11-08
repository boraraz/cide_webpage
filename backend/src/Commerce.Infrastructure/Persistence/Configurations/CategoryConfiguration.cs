using Commerce.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Name).HasMaxLength(PublishRules.CategoryNameMaxLength).IsRequired();
        builder.Property(category => category.Slug).HasMaxLength(Slug.MaxLength).IsRequired();
        builder.Property(category => category.Description).HasMaxLength(PublishRules.DescriptionOptionalMaxLength);
        builder.HasIndex(category => category.Slug).IsUnique();
        builder.HasIndex(category => category.ParentCategoryId);
        builder.HasIndex(category => new { category.IsActive, category.DisplayOrder });
        builder.HasOne(category => category.Parent)
            .WithMany(category => category.Children)
            .HasForeignKey(category => category.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
