using System.ComponentModel.DataAnnotations;

namespace Commerce.Infrastructure.Identity;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string? Host { get; set; }

    public int Port { get; set; } = 1025;

    public string FromAddress { get; set; } = "noreply@localhost";

    public string FromName { get; set; } = "Sportswear";
}

public sealed class IdentitySeedOptions
{
    public const string SectionName = "Identity";

    public bool SeedRoles { get; set; }

    public bool BootstrapAdmin { get; set; }

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }
}

public sealed class PublicUrlOptions
{
    public const string SectionName = "PublicUrls";

    [Required]
    public string StorefrontUrl { get; set; } = "http://localhost:5173";
}

public sealed class AuthLimitOptions
{
    public const string SectionName = "Auth";

    public int SessionDays { get; set; } = 14;

    public int EmailVerificationHours { get; set; } = 24;

    public int PasswordResetHours { get; set; } = 1;

    public int SecurityEventRetentionDays { get; set; } = 180;

    public int RegisterPermitLimit { get; set; } = 5;

    public int RegisterWindowMinutes { get; set; } = 60;

    public int LoginPermitLimit { get; set; } = 10;

    public int LoginWindowMinutes { get; set; } = 1;

    public int VerificationPermitLimit { get; set; } = 3;

    public int VerificationWindowMinutes { get; set; } = 60;

    public int PasswordResetPermitLimit { get; set; } = 3;

    public int PasswordResetWindowMinutes { get; set; } = 60;
}
