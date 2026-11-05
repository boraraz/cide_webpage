using System.Security.Cryptography;
using System.Text;

namespace Commerce.Application.Identity;

/// <summary>
/// Random tokens from <see cref="RandomNumberGenerator"/> and SHA-256 hashes for storage.
/// Encoding is base64url so tokens are safe in a cookie or a query string.
/// </summary>
public static class SecureTokens
{
    public static string Create() => Encode(RandomNumberGenerator.GetBytes(32));

    public static byte[] Hash(string rawToken) => SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));

    private static string Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
