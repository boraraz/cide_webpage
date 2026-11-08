using Commerce.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class SlugHistoryConfiguration : IEntityTypeConfiguration<SlugHistory>
{
    public void Configure(EntityTypeBuilder<SlugHistory> builder)
    {
        builder.HasKey(history => history.Id);
        builder.Property(history => history.OwnerType)
            .HasConversion(
                owner => owner.ToString().ToLowerInvariant(),
                value => Enum.Parse<SlugOwner>(value, true))
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(history => history.Slug).HasMaxLength(Slug.MaxLength).IsRequired();
        builder.HasIndex(history => new { history.OwnerType, history.Slug }).IsUnique();
        builder.HasIndex(history => new { history.OwnerType, history.OwnerId });
    }
}
