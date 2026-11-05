using Commerce.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context for the modular monolith. Entity configurations are discovered
/// from this assembly; migrations are stored in Persistence/Migrations.
/// </summary>
public sealed class CommerceDbContext(DbContextOptions<CommerceDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();

    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommerceDbContext).Assembly);
    }
}
