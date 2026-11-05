namespace Commerce.Domain.Identity;

/// <summary>A security audit row. It never contains passwords, tokens or email addresses.</summary>
public sealed class SecurityEvent
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public SecurityEventType EventType { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }
}
