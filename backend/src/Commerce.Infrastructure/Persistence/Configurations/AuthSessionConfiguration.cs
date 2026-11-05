using Commerce.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSession>
{
    public void Configure(EntityTypeBuilder<AuthSession> builder)
    {
        builder.HasKey(session => session.Id);
        builder.Property(session => session.TokenHash).HasMaxLength(32).IsRequired();
        builder.HasIndex(session => session.TokenHash).IsUnique();
        builder.HasIndex(session => session.UserId);
        builder.HasIndex(session => session.FamilyId);
        builder.Property(session => session.UserAgent).HasMaxLength(256);
        builder.Property(session => session.IpAddress).HasMaxLength(64);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AuthSession>()
            .WithMany()
            .HasForeignKey(session => session.ReplacedBySessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
