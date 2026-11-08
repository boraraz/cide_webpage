using Commerce.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class ProductOptionValueConfiguration : IEntityTypeConfiguration<ProductOptionValue>
{
    public void Configure(EntityTypeBuilder<ProductOptionValue> builder)
    {
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Value).HasMaxLength(PublishRules.OptionNameMaxLength).IsRequired();
        builder.Property(value => value.NormalizedValue).HasMaxLength(Slug.MaxLength).IsRequired();
        builder.Property(value => value.Code).HasMaxLength(PublishRules.CodeMaxLength);
        builder.HasIndex(value => new { value.OptionId, value.NormalizedValue }).IsUnique();
        builder.HasOne(value => value.Option)
            .WithMany(option => option.Values)
            .HasForeignKey(value => value.OptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
