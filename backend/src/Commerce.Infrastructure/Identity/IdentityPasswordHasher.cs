using Commerce.Application.Identity;
using Commerce.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace Commerce.Infrastructure.Identity;

internal sealed class IdentityPasswordHasher(IPasswordHasher<User> hasher) : IPasswordHasher
{
    public string Hash(User user, string password) => hasher.HashPassword(user, password);

    public PasswordCheck Verify(User user, string password)
    {
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return new PasswordCheck(
            result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded,
            result == PasswordVerificationResult.SuccessRehashNeeded);
    }
}
