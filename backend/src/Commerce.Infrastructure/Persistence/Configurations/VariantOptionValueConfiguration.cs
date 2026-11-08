using Commerce.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class VariantOptionValueConfiguration : IEntityTypeConfiguration<VariantOptionValue>
{
    public void Configure(EntityTypeBuilder<VariantOptionValue> builder)
    {
        builder.HasKey(selection => new { selection.VariantId, selection.OptionValueId });
        builder.HasOne(selection => selection.Variant)
            .WithMany(variant => variant.Selections)
            .HasForeignKey(selection => selection.VariantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(selection => selection.OptionValue)
            .WithMany(value => value.VariantSelections)
            .HasForeignKey(selection => selection.OptionValueId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
