namespace Commerce.Domain.Identity;

/// <summary>
/// A server-side browser session. Only the SHA-256 hash of the cookie token is stored.
/// </summary>
public sealed class AuthSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public byte[] TokenHash { get; set; } = [];

    /// <summary>Groups a session with the tokens that rotated it. Reuse of an old token revokes the family.</summary>
    public Guid FamilyId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public Guid? ReplacedBySessionId { get; set; }

    public string? UserAgent { get; set; }

    public string? IpAddress { get; set; }
}
