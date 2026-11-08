using Commerce.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class InventoryBalanceConfiguration : IEntityTypeConfiguration<InventoryBalance>
{
    public void Configure(EntityTypeBuilder<InventoryBalance> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_inventory_balances_on_hand_nonnegative", "on_hand >= 0");
            table.HasCheckConstraint("ck_inventory_balances_reserved_nonnegative", "reserved >= 0");
            table.HasCheckConstraint("ck_inventory_balances_reserved_within_on_hand", "reserved <= on_hand");
        });
        builder.HasKey(balance => balance.VariantId);
        builder.Ignore(balance => balance.Available);
        builder.HasOne(balance => balance.Variant)
            .WithOne(variant => variant.Balance)
            .HasForeignKey<InventoryBalance>(balance => balance.VariantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
