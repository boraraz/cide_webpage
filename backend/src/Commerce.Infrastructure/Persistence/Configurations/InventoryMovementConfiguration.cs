using Commerce.Domain.Catalog;
using Commerce.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.HasKey(movement => movement.Id);
        builder.Property(movement => movement.MovementType)
            .HasConversion(
                type => type.ToString().ToLowerInvariant(),
                value => Enum.Parse<InventoryMovementType>(value, true))
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(movement => movement.QuantityDelta);
        builder.Property(movement => movement.ReservedDelta).HasDefaultValue(0);
        builder.Property(movement => movement.Note).HasMaxLength(PublishRules.NoteMaxLength);
        builder.Property(movement => movement.IdempotencyKey).HasMaxLength(80).IsRequired();
        builder.Property(movement => movement.RequestHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(movement => new { movement.VariantId, movement.IdempotencyKey }).IsUnique();
        builder.HasIndex(movement => new { movement.VariantId, movement.CreatedAt });
        builder.HasOne(movement => movement.Variant)
            .WithMany()
            .HasForeignKey(movement => movement.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(movement => movement.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
