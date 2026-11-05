using Commerce.Domain.Identity;

namespace Commerce.UnitTests.Identity;

public sealed class IdentityRulesTests
{
    [Fact]
    public void Email_is_normalized_case_insensitively()
    {
        var canonical = EmailNormalizer.Canonicalize("  Ada@Example.com ");
        Assert.Equal(("Ada@Example.com", "ADA@EXAMPLE.COM"), canonical);
        Assert.Null(EmailNormalizer.Canonicalize("not-an-email"));
    }

    [Fact]
    public void Password_policy_rejects_short_and_unconfirmed_passwords()
    {
        var errors = PasswordRules.Validate("short", "short", "ada@example.com");
        Assert.Contains("password", errors.Keys);

        errors = PasswordRules.Validate("passwordonly", "passwordonly", "ada@example.com");
        Assert.Contains("password", errors.Keys);

        errors = PasswordRules.Validate("Password123", "different", "ada@example.com");
        Assert.Contains("passwordConfirmation", errors.Keys);

        Assert.Empty(PasswordRules.Validate("Password123", "Password123", "ada@example.com"));
    }

    [Theory]
    [InlineData("+90 532 000 00 00", "+905320000000")]
    [InlineData("05320000000", "+905320000000")]
    [InlineData("5320000000", "+905320000000")]
    public void Turkish_numbers_normalize_to_e164(string input, string expected)
    {
        Assert.True(TurkishPhoneNumber.TryNormalize(input, out var normalized, out _));
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void Blank_phone_is_allowed_and_foreign_numbers_are_not()
    {
        Assert.True(TurkishPhoneNumber.TryNormalize("  ", out var normalized, out var error));
        Assert.Null(normalized);
        Assert.Null(error);
        Assert.False(TurkishPhoneNumber.TryNormalize("+1 202 555 0100", out _, out error));
        Assert.NotNull(error);
    }
}
