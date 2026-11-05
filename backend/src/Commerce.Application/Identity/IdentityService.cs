using Commerce.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Commerce.Application.Identity;

public sealed partial class IdentityService(
    IUserAccounts accounts,
    IAuthSessions sessions,
    IOneTimeTokens tokens,
    ISecurityEvents securityEvents,
    IEmailSender email,
    IPasswordHasher passwords,
    AuthPolicy policy,
    TimeProvider time,
    ILogger<IdentityService> logger)
{
    public async Task<IdentityResult<AuthSuccess>> RegisterAsync(
        string? firstName,
        string? lastName,
        string? email,
        string? password,
        string? passwordConfirmation,
        ClientInfo client,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var given = PersonName.Validate(firstName, "firstName", "first name", errors);
        var family = PersonName.Validate(lastName, "lastName", "last name", errors);
        var canonical = EmailNormalizer.Canonicalize(email);
        if (canonical is null)
        {
            errors["email"] = ["Enter a valid email address."];
        }

        foreach (var (field, messages) in PasswordRules.Validate(password, passwordConfirmation, canonical?.Email))
        {
            errors[field] = messages;
        }

        if (errors.Count > 0)
        {
            return IdentityResult<AuthSuccess>.Fail(Validation(errors));
        }

        if (await accounts.FindByNormalizedEmailAsync(canonical!.Value.Normalized, cancellationToken) is not null)
        {
            return IdentityResult<AuthSuccess>.Fail(new IdentityFailure(
                Status: 409,
                Title: "An account with this email already exists."));
        }

        var role = await RequireRoleAsync(RoleNames.Customer, cancellationToken);
        var now = time.GetUtcNow();
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = canonical.Value.Email,
            NormalizedEmail = canonical.Value.Normalized,
            FirstName = given!,
            LastName = family!,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        user.PasswordHash = passwords.Hash(user, password!);
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, User = user, Role = role });
        await accounts.AddAsync(user, cancellationToken);
        await RecordAsync(SecurityEventType.Registration, user.Id, client, now, cancellationToken);

        var rawVerification = await IssueEmailVerificationAsync(user, now, cancellationToken);
        var issued = await IssueSessionAsync(user, client, now, cancellationToken);
        await accounts.SaveChangesAsync(cancellationToken);
        await TrySendAsync(user.Id, VerificationMessage(user, rawVerification), cancellationToken);
        return IdentityResult<AuthSuccess>.Ok(issued);
    }

    public async Task<IdentityResult<AuthSuccess>> LoginAsync(
        string? email,
        string? password,
        string requiredRole,
        ClientInfo client,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return IdentityResult<AuthSuccess>.Fail(Validation(new Dictionary<string, string[]>
            {
                ["email"] = ["Enter your email and password."],
            }));
        }

        var canonical = EmailNormalizer.Canonicalize(email);
        var now = time.GetUtcNow();
        var user = canonical is null
            ? null
            : await accounts.FindByNormalizedEmailAsync(canonical.Value.Normalized, cancellationToken);

        var check = user is null ? default : passwords.Verify(user, password);
        var roleMatches = user?.UserRoles.Any(assignment => assignment.Role.Name == requiredRole) == true;
        if (user is null || !user.IsActive || !roleMatches || !check.Succeeded)
        {
            await RecordAsync(SecurityEventType.LoginFailed, user?.Id, client, now, cancellationToken);
            await accounts.SaveChangesAsync(cancellationToken);
            return IdentityResult<AuthSuccess>.Fail(InvalidCredentials());
        }

        if (check.NeedsRehash)
        {
            user.PasswordHash = passwords.Hash(user, password);
        }

        user.LastLoginAt = now;
        user.UpdatedAt = now;
        await RecordAsync(SecurityEventType.LoginSucceeded, user.Id, client, now, cancellationToken);
        var issued = await IssueSessionAsync(user, client, now, cancellationToken);
        await accounts.SaveChangesAsync(cancellationToken);
        return IdentityResult<AuthSuccess>.Ok(issued);
    }

    public async Task<ResolvedSession?> ResolveSessionAsync(string rawToken, CancellationToken cancellationToken)
    {
        var session = await sessions.FindByTokenHashAsync(SecureTokens.Hash(rawToken), cancellationToken);
        if (session is null)
        {
            return null;
        }

        var now = time.GetUtcNow();
        if (session.ReplacedBySessionId is not null)
        {
            await sessions.RevokeFamilyAsync(session.FamilyId, now, cancellationToken);
            await RecordAsync(SecurityEventType.SessionRevoked, session.UserId, ClientInfo.Empty, now, cancellationToken);
            await accounts.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (session.RevokedAt is not null || session.ExpiresAt <= now)
        {
            return null;
        }

        var user = await accounts.FindByIdAsync(session.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        string? rotatedToken = null;
        DateTimeOffset? rotatedExpires = null;
        var lifetime = session.ExpiresAt - session.CreatedAt;
        if (lifetime > TimeSpan.Zero && now - session.CreatedAt > lifetime / 2)
        {
            var replacement = NewSession(user.Id, session.FamilyId, session.ExpiresAt, now, session.UserAgent, session.IpAddress);
            rotatedToken = replacement.RawToken;
            rotatedExpires = replacement.Session.ExpiresAt;
            session.RevokedAt = now;
            session.ReplacedBySessionId = replacement.Session.Id;
            await sessions.AddAsync(replacement.Session, cancellationToken);
            await accounts.SaveChangesAsync(cancellationToken);
        }

        return new ResolvedSession(ToCurrentUser(user), rotatedToken, rotatedExpires);
    }

    public async Task LogoutAsync(string? rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(rawToken))
        {
            return;
        }

        var session = await sessions.FindByTokenHashAsync(SecureTokens.Hash(rawToken), cancellationToken);
        if (session is null || session.RevokedAt is not null)
        {
            return;
        }

        var now = time.GetUtcNow();
        session.RevokedAt = now;
        await RecordAsync(SecurityEventType.SessionRevoked, session.UserId, ClientInfo.Empty, now, cancellationToken);
        await accounts.SaveChangesAsync(cancellationToken);
    }

    public async Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await sessions.RevokeAllForUserAsync(userId, now, cancellationToken);
        await RecordAsync(SecurityEventType.SessionRevoked, userId, ClientInfo.Empty, now, cancellationToken);
        await accounts.SaveChangesAsync(cancellationToken);
    }

    public async Task<CurrentUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await accounts.FindByIdAsync(userId, cancellationToken);
        return user is null || !user.IsActive ? null : ToCurrentUser(user);
    }

    public async Task<IdentityResult<CurrentUser>> UpdateProfileAsync(
        Guid userId,
        string? firstName,
        string? lastName,
        string? phoneNumber,
        CancellationToken cancellationToken)
    {
        var user = await accounts.FindByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return IdentityResult<CurrentUser>.Fail(new IdentityFailure(401, "Authentication is required."));
        }

        var errors = new Dictionary<string, string[]>();
        var given = PersonName.Validate(firstName, "firstName", "first name", errors);
        var family = PersonName.Validate(lastName, "lastName", "last name", errors);
        if (!TurkishPhoneNumber.TryNormalize(phoneNumber, out var normalizedPhone, out var phoneError))
        {
            errors["phoneNumber"] = [phoneError!];
        }

        if (errors.Count > 0)
        {
            return IdentityResult<CurrentUser>.Fail(Validation(errors));
        }

        user.FirstName = given!;
        user.LastName = family!;
        user.PhoneNumber = normalizedPhone;
        user.UpdatedAt = time.GetUtcNow();
        await accounts.SaveChangesAsync(cancellationToken);
        return IdentityResult<CurrentUser>.Ok(ToCurrentUser(user));
    }

    public async Task<IdentityResult<bool>> ChangePasswordAsync(
        Guid userId,
        string? currentPassword,
        string? newPassword,
        string? confirmation,
        CancellationToken cancellationToken)
    {
        var user = await accounts.FindByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return IdentityResult<bool>.Fail(new IdentityFailure(401, "Authentication is required."));
        }

        if (string.IsNullOrEmpty(currentPassword) || !passwords.Verify(user, currentPassword).Succeeded)
        {
            return IdentityResult<bool>.Fail(Validation(new Dictionary<string, string[]>
            {
                ["currentPassword"] = ["The current password is incorrect."],
            }));
        }

        var errors = PasswordRules.Validate(newPassword, confirmation, user.Email);
        if (errors.Count > 0)
        {
            return IdentityResult<bool>.Fail(Validation(errors));
        }

        var now = time.GetUtcNow();
        user.PasswordHash = passwords.Hash(user, newPassword!);
        user.UpdatedAt = now;
        await sessions.RevokeAllForUserAsync(user.Id, now, cancellationToken);
        await RecordAsync(SecurityEventType.PasswordChanged, user.Id, ClientInfo.Empty, now, cancellationToken);
        await accounts.SaveChangesAsync(cancellationToken);
        return IdentityResult<bool>.Ok(true);
    }

    public async Task RequestEmailVerificationAsync(string? email, Guid? currentUserId, CancellationToken cancellationToken)
    {
        User? user = null;
        if (currentUserId is Guid userId)
        {
            user = await accounts.FindByIdAsync(userId, cancellationToken);
        }
        else if (EmailNormalizer.Canonicalize(email) is { } canonical)
        {
            user = await accounts.FindByNormalizedEmailAsync(canonical.Normalized, cancellationToken);
        }

        if (user is null || !user.IsActive || user.EmailVerifiedAt is not null)
        {
            return;
        }

        var now = time.GetUtcNow();
        var raw = await IssueEmailVerificationAsync(user, now, cancellationToken);
        await accounts.SaveChangesAsync(cancellationToken);
        await TrySendAsync(user.Id, VerificationMessage(user, raw), cancellationToken);
    }

    public async Task<IdentityResult<bool>> ConfirmEmailVerificationAsync(string? rawToken, CancellationToken cancellationToken)
    {
        var token = await FindUsableEmailTokenAsync(rawToken, cancellationToken);
        if (token is null)
        {
            return IdentityResult<bool>.Fail(InvalidToken("This verification link is invalid or has expired."));
        }

        var user = await accounts.FindByIdAsync(token.UserId, cancellationToken);
        if (user is null)
        {
            return IdentityResult<bool>.Fail(InvalidToken("This verification link is invalid or has expired."));
        }

        var now = time.GetUtcNow();
        token.ConsumedAt = now;
        user.EmailVerifiedAt ??= now;
        user.UpdatedAt = now;
        await accounts.SaveChangesAsync(cancellationToken);
        return IdentityResult<bool>.Ok(true);
    }

    public async Task RequestPasswordResetAsync(string? email, CancellationToken cancellationToken)
    {
        if (EmailNormalizer.Canonicalize(email) is not { } canonical)
        {
            return;
        }

        var user = await accounts.FindByNormalizedEmailAsync(canonical.Normalized, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return;
        }

        var now = time.GetUtcNow();
        await tokens.InvalidateActivePasswordResetsAsync(user.Id, now, cancellationToken);
        var raw = SecureTokens.Create();
        await tokens.AddPasswordResetAsync(new PasswordResetToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            TokenHash = SecureTokens.Hash(raw),
            CreatedAt = now,
            ExpiresAt = now.AddHours(policy.PasswordResetHours),
        }, cancellationToken);
        await accounts.SaveChangesAsync(cancellationToken);

        var link = $"{policy.StorefrontUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(raw)}";
        await TrySendAsync(user.Id, new EmailMessage(
            user.Email,
            "Reset your Sportswear password",
            $"A password reset was requested. Open this link to choose a new password: {link}\n\nIf you did not ask for this, you can ignore the message."),
            cancellationToken);
    }

    public async Task<IdentityResult<bool>> ConfirmPasswordResetAsync(
        string? rawToken,
        string? password,
        string? confirmation,
        CancellationToken cancellationToken)
    {
        var token = await FindUsableResetTokenAsync(rawToken, cancellationToken);
        if (token is null)
        {
            return IdentityResult<bool>.Fail(InvalidToken("This reset link is invalid or has expired."));
        }

        var user = await accounts.FindByIdAsync(token.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return IdentityResult<bool>.Fail(InvalidToken("This reset link is invalid or has expired."));
        }

        var errors = PasswordRules.Validate(password, confirmation, user.Email);
        if (errors.Count > 0)
        {
            return IdentityResult<bool>.Fail(Validation(errors));
        }

        var now = time.GetUtcNow();
        token.ConsumedAt = now;
        user.PasswordHash = passwords.Hash(user, password!);
        user.UpdatedAt = now;
        await sessions.RevokeAllForUserAsync(user.Id, now, cancellationToken);
        await RecordAsync(SecurityEventType.PasswordReset, user.Id, ClientInfo.Empty, now, cancellationToken);
        await accounts.SaveChangesAsync(cancellationToken);
        return IdentityResult<bool>.Ok(true);
    }

    private async Task<string> IssueEmailVerificationAsync(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await tokens.InvalidateActiveEmailVerificationsAsync(user.Id, now, cancellationToken);
        var raw = SecureTokens.Create();
        await tokens.AddEmailVerificationAsync(new EmailVerificationToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            TokenHash = SecureTokens.Hash(raw),
            CreatedAt = now,
            ExpiresAt = now.AddHours(policy.EmailVerificationHours),
        }, cancellationToken);
        return raw;
    }

    private async Task<EmailVerificationToken?> FindUsableEmailTokenAsync(string? rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var token = await tokens.FindEmailVerificationAsync(SecureTokens.Hash(rawToken), cancellationToken);
        return token is null || token.ConsumedAt is not null || token.ExpiresAt <= time.GetUtcNow() ? null : token;
    }

    private async Task<PasswordResetToken?> FindUsableResetTokenAsync(string? rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var token = await tokens.FindPasswordResetAsync(SecureTokens.Hash(rawToken), cancellationToken);
        return token is null || token.ConsumedAt is not null || token.ExpiresAt <= time.GetUtcNow() ? null : token;
    }

    private async Task<AuthSuccess> IssueSessionAsync(
        User user,
        ClientInfo client,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var created = NewSession(
            user.Id,
            Guid.CreateVersion7(),
            now.AddDays(policy.SessionDays),
            now,
            Truncate(client.UserAgent, 256),
            Truncate(client.IpAddress, 64));
        await sessions.AddAsync(created.Session, cancellationToken);
        return new AuthSuccess(ToCurrentUser(user), created.RawToken, created.Session.ExpiresAt);
    }

    private static (AuthSession Session, string RawToken) NewSession(
        Guid userId,
        Guid familyId,
        DateTimeOffset expiresAt,
        DateTimeOffset now,
        string? userAgent,
        string? ipAddress)
    {
        var raw = SecureTokens.Create();
        return (new AuthSession
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TokenHash = SecureTokens.Hash(raw),
            FamilyId = familyId,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            UserAgent = userAgent,
            IpAddress = ipAddress,
        }, raw);
    }

    private async Task<Role> RequireRoleAsync(string name, CancellationToken cancellationToken)
    {
        var role = await accounts.FindRoleByNameAsync(name, cancellationToken);
        if (role is null)
        {
            throw new InvalidOperationException($"The {name} role has not been seeded.");
        }

        return role;
    }

    private async Task RecordAsync(
        SecurityEventType type,
        Guid? userId,
        ClientInfo client,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        await securityEvents.AddAsync(new SecurityEvent
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            EventType = type,
            OccurredAt = occurredAt,
            IpAddress = Truncate(client.IpAddress, 64),
            UserAgent = Truncate(client.UserAgent, 256),
        }, cancellationToken);
    }

    private EmailMessage VerificationMessage(User user, string rawToken)
    {
        var link = $"{policy.StorefrontUrl.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(rawToken)}";
        return new EmailMessage(
            user.Email,
            "Verify your Sportswear email",
            $"Confirm this address to finish setting up your account: {link}");
    }

    private async Task TrySendAsync(Guid userId, EmailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await email.SendAsync(message, cancellationToken);
        }
        catch (Exception exception)
        {
            LogEmailFailed(logger, exception, userId);
        }
    }

    private static CurrentUser ToCurrentUser(User user) => new(
        user.Id,
        user.Email,
        user.FirstName,
        user.LastName,
        user.PhoneNumber,
        user.EmailVerifiedAt is not null,
        user.IsActive,
        user.UserRoles.Select(assignment => assignment.Role.Name).Order(StringComparer.Ordinal).ToArray());

    private static IdentityFailure Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(400, "The request is not valid.", Errors: errors);

    private static IdentityFailure InvalidCredentials() =>
        new(401, "The email or password is incorrect.");

    private static IdentityFailure InvalidToken(string title) => new(400, title);

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];

    [LoggerMessage(Level = LogLevel.Error, Message = "Identity email could not be sent for user {UserId}")]
    private static partial void LogEmailFailed(ILogger logger, Exception exception, Guid userId);
}
