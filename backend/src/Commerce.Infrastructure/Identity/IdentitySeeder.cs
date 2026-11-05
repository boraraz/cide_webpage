using Commerce.Application.Identity;
using Commerce.Domain.Identity;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Identity;

public sealed class IdentitySeeder(
    CommerceDbContext database,
    IPasswordHasher passwords,
    IOptions<IdentitySeedOptions> seedOptions,
    TimeProvider time)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var options = seedOptions.Value;
        if (options.SeedRoles)
        {
            await EnsureRoleAsync(RoleNames.Customer, cancellationToken);
            await EnsureRoleAsync(RoleNames.Admin, cancellationToken);
        }

        if (!options.BootstrapAdmin)
        {
            return;
        }

        var canonical = EmailNormalizer.Canonicalize(options.AdminEmail)
            ?? throw new InvalidOperationException("Identity:AdminEmail is not a valid email address.");
        var passwordErrors = PasswordRules.Validate(options.AdminPassword, options.AdminPassword, canonical.Email);
        if (passwordErrors.Count > 0)
        {
            throw new InvalidOperationException("Identity:AdminPassword does not meet the password policy.");
        }

        var adminRole = await database.Roles.SingleAsync(role => role.Name == RoleNames.Admin, cancellationToken);
        var user = await database.Users
            .Include(candidate => candidate.UserRoles)
            .FirstOrDefaultAsync(candidate => candidate.NormalizedEmail == canonical.Normalized, cancellationToken);

        if (user is null)
        {
            var now = time.GetUtcNow();
            user = new User
            {
                Id = Guid.CreateVersion7(),
                Email = canonical.Email,
                NormalizedEmail = canonical.Normalized,
                FirstName = "Admin",
                LastName = "User",
                IsActive = true,
                EmailVerifiedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            };
            user.PasswordHash = passwords.Hash(user, options.AdminPassword!);
            database.Users.Add(user);
            database.SecurityEvents.Add(new SecurityEvent
            {
                Id = Guid.CreateVersion7(),
                UserId = user.Id,
                EventType = SecurityEventType.AdminBootstrapped,
                OccurredAt = now,
            });
        }

        if (user.UserRoles.All(assignment => assignment.RoleId != adminRole.Id))
        {
            database.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureRoleAsync(string name, CancellationToken cancellationToken)
    {
        if (await database.Roles.AnyAsync(role => role.Name == name, cancellationToken))
        {
            return;
        }

        database.Roles.Add(new Role { Id = Guid.CreateVersion7(), Name = name });
        await database.SaveChangesAsync(cancellationToken);
    }
}
