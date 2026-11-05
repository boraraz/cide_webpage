using Commerce.Application.Identity;
using Commerce.Domain.Identity;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Identity;

internal sealed class IdentityStore(CommerceDbContext database) :
    IUserAccounts,
    IAuthSessions,
    IOneTimeTokens,
    ISecurityEvents
{
    public Task<User?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        UsersWithRoles().FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        UsersWithRoles().FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task AddAsync(User user, CancellationToken cancellationToken) =>
        database.Users.AddAsync(user, cancellationToken).AsTask();

    public Task<Role?> FindRoleByNameAsync(string name, CancellationToken cancellationToken) =>
        database.Roles.FirstOrDefaultAsync(role => role.Name == name, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        database.SaveChangesAsync(cancellationToken);

    public Task AddAsync(AuthSession session, CancellationToken cancellationToken) =>
        database.AuthSessions.AddAsync(session, cancellationToken).AsTask();

    public Task<AuthSession?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken) =>
        database.AuthSessions.FirstOrDefaultAsync(session => session.TokenHash == tokenHash, cancellationToken);

    public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset revokedAt, CancellationToken cancellationToken) =>
        database.AuthSessions
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAt, revokedAt), cancellationToken);

    public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken) =>
        database.AuthSessions
            .Where(session => session.FamilyId == familyId && session.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAt, revokedAt), cancellationToken);

    public Task InvalidateActiveEmailVerificationsAsync(Guid userId, DateTimeOffset consumedAt, CancellationToken cancellationToken) =>
        database.EmailVerificationTokens
            .Where(token => token.UserId == userId && token.ConsumedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.ConsumedAt, consumedAt), cancellationToken);

    public Task AddEmailVerificationAsync(EmailVerificationToken token, CancellationToken cancellationToken) =>
        database.EmailVerificationTokens.AddAsync(token, cancellationToken).AsTask();

    public Task<EmailVerificationToken?> FindEmailVerificationAsync(byte[] tokenHash, CancellationToken cancellationToken) =>
        database.EmailVerificationTokens.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public Task InvalidateActivePasswordResetsAsync(Guid userId, DateTimeOffset consumedAt, CancellationToken cancellationToken) =>
        database.PasswordResetTokens
            .Where(token => token.UserId == userId && token.ConsumedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.ConsumedAt, consumedAt), cancellationToken);

    public Task AddPasswordResetAsync(PasswordResetToken token, CancellationToken cancellationToken) =>
        database.PasswordResetTokens.AddAsync(token, cancellationToken).AsTask();

    public Task<PasswordResetToken?> FindPasswordResetAsync(byte[] tokenHash, CancellationToken cancellationToken) =>
        database.PasswordResetTokens.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public Task AddAsync(SecurityEvent securityEvent, CancellationToken cancellationToken) =>
        database.SecurityEvents.AddAsync(securityEvent, cancellationToken).AsTask();

    private IQueryable<User> UsersWithRoles() =>
        database.Users.Include(user => user.UserRoles).ThenInclude(assignment => assignment.Role);
}
