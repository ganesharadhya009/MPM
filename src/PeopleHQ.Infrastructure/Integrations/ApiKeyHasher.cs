using System.Security.Cryptography;
using System.Text;

namespace PeopleHQ.Infrastructure.Integrations;

/// <summary>Generates and hashes tenant API keys. The plaintext is never persisted — only Hash(plaintext)
/// is stored, checked against on each future request (once the inbound auth handler is built).</summary>
public static class ApiKeyHasher
{
    private const string Prefix = "phq_";

    public static (string Plaintext, string KeyPrefix) GenerateKey()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(randomBytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var plaintext = Prefix + token;
        return (plaintext, plaintext[..Math.Min(12, plaintext.Length)]);
    }

    public static string Hash(string plaintextKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintextKey));
        return Convert.ToHexString(bytes);
    }
}
