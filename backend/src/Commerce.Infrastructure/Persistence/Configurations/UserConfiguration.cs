using Commerce.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Email).HasMaxLength(EmailNormalizer.MaxLength).IsRequired();
        builder.Property(user => user.NormalizedEmail).HasMaxLength(EmailNormalizer.MaxLength).IsRequired();
        builder.HasIndex(user => user.NormalizedEmail).IsUnique();
        builder.Property(user => user.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(user => user.FirstName).HasMaxLength(PersonName.MaxLength).IsRequired();
        builder.Property(user => user.LastName).HasMaxLength(PersonName.MaxLength).IsRequired();
        builder.Property(user => user.PhoneNumber).HasMaxLength(16);

        builder.HasMany(user => user.UserRoles)
            .WithOne(assignment => assignment.User)
            .HasForeignKey(assignment => assignment.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
