using Commerce.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    public void Configure(EntityTypeBuilder<SecurityEvent> builder)
    {
        builder.HasKey(securityEvent => securityEvent.Id);
        builder.Property(securityEvent => securityEvent.EventType).HasConversion<string>().HasMaxLength(40);
        builder.Property(securityEvent => securityEvent.UserAgent).HasMaxLength(256);
        builder.Property(securityEvent => securityEvent.IpAddress).HasMaxLength(64);
        builder.HasIndex(securityEvent => new { securityEvent.UserId, securityEvent.OccurredAt });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(securityEvent => securityEvent.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
