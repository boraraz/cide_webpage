using Commerce.Domain.Catalog;
using Commerce.Domain.Content;
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

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Collection> Collections => Set<Collection>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();

    public DbSet<ProductCollection> ProductCollections => Set<ProductCollection>();

    public DbSet<ProductOption> ProductOptions => Set<ProductOption>();

    public DbSet<ProductOptionValue> ProductOptionValues => Set<ProductOptionValue>();

    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

    public DbSet<VariantOptionValue> VariantOptionValues => Set<VariantOptionValue>();

    public DbSet<InventoryBalance> InventoryBalances => Set<InventoryBalance>();

    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    public DbSet<ProductMedia> ProductMedia => Set<ProductMedia>();

    public DbSet<SlugHistory> SlugHistories => Set<SlugHistory>();

    public DbSet<ContentPage> ContentPages => Set<ContentPage>();

    public DbSet<ContentRevision> ContentRevisions => Set<ContentRevision>();

    public DbSet<ContentMedia> ContentMedia => Set<ContentMedia>();

    public DbSet<ContentRevisionMedia> ContentRevisionMedia => Set<ContentRevisionMedia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommerceDbContext).Assembly);
    }
}
