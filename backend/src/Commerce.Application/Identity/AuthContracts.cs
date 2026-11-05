using Commerce.Domain.Identity;

namespace Commerce.Application.Identity;

public sealed record ClientInfo(string? IpAddress, string? UserAgent)
{
    public static ClientInfo Empty { get; } = new(null, null);
}

public sealed record CurrentUser(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    bool EmailVerified,
    bool IsActive,
    IReadOnlyList<string> Roles);

public sealed record AuthSuccess(CurrentUser User, string SessionToken, DateTimeOffset SessionExpiresAt);

public sealed record ResolvedSession(CurrentUser User, string? RotatedToken, DateTimeOffset? RotatedExpiresAt);

public sealed record IdentityFailure(
    int Status,
    string Title,
    string? Detail = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed record IdentityResult<T>(T? Value, IdentityFailure? Failure)
{
    public bool Succeeded => Failure is null;

    public static IdentityResult<T> Ok(T value) => new(value, null);

    public static IdentityResult<T> Fail(IdentityFailure failure) => new(default, failure);
}

public sealed record AuthPolicy(
    int SessionDays,
    int EmailVerificationHours,
    int PasswordResetHours,
    string StorefrontUrl);

public interface IUserAccounts
{
    Task<User?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(User user, CancellationToken cancellationToken);

    Task<Role?> FindRoleByNameAsync(string name, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IAuthSessions
{
    Task AddAsync(AuthSession session, CancellationToken cancellationToken);

    Task<AuthSession?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken);

    Task RevokeAllForUserAsync(Guid userId, DateTimeOffset revokedAt, CancellationToken cancellationToken);

    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken);
}

public interface IOneTimeTokens
{
    Task InvalidateActiveEmailVerificationsAsync(Guid userId, DateTimeOffset consumedAt, CancellationToken cancellationToken);

    Task AddEmailVerificationAsync(EmailVerificationToken token, CancellationToken cancellationToken);

    Task<EmailVerificationToken?> FindEmailVerificationAsync(byte[] tokenHash, CancellationToken cancellationToken);

    Task InvalidateActivePasswordResetsAsync(Guid userId, DateTimeOffset consumedAt, CancellationToken cancellationToken);

    Task AddPasswordResetAsync(PasswordResetToken token, CancellationToken cancellationToken);

    Task<PasswordResetToken?> FindPasswordResetAsync(byte[] tokenHash, CancellationToken cancellationToken);
}

public interface ISecurityEvents
{
    Task AddAsync(SecurityEvent securityEvent, CancellationToken cancellationToken);
}

public sealed record EmailMessage(string To, string Subject, string Text);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public readonly record struct PasswordCheck(bool Succeeded, bool NeedsRehash);

public interface IPasswordHasher
{
    string Hash(User user, string password);

    PasswordCheck Verify(User user, string password);
}
