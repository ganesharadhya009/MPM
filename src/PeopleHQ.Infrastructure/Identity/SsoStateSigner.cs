using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace PeopleHQ.Infrastructure.Identity;

/// <summary>
/// Signs and validates the OIDC "state" parameter as a self-contained, short-lived token — avoids a new DB
/// table for pending-login tracking. Payload is "{tenantId}|{nonce}|{expiryUnixSeconds}", HMAC-SHA256'd
/// with the same Jwt:SigningKey used for access tokens (no new secret to provision). Binding tenantId into
/// the signed payload and checking it against the CURRENT request's resolved tenant at validation time
/// prevents a state minted for one tenant from being replayed against another tenant's callback endpoint.
/// </summary>
public class SsoStateSigner
{
    private readonly IConfiguration _config;
    public SsoStateSigner(IConfiguration config) => _config = config;

    public string Create(Guid tenantId, TimeSpan validFor)
    {
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var expiry = DateTimeOffset.UtcNow.Add(validFor).ToUnixTimeSeconds();
        var payload = $"{tenantId}|{nonce}|{expiry}";
        var signature = Sign(payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{payload}|{signature}"))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public bool TryValidate(string state, Guid expectedTenantId)
    {
        try
        {
            var padded = state.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - padded.Length % 4) % 4);
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var parts = decoded.Split('|');
            if (parts.Length != 4) return false;

            var (tenantIdPart, _, expiryPart, signaturePart) = (parts[0], parts[1], parts[2], parts[3]);
            var payload = $"{parts[0]}|{parts[1]}|{parts[2]}";
            if (!FixedTimeEquals(Sign(payload), signaturePart)) return false;

            if (!Guid.TryParse(tenantIdPart, out var tenantId) || tenantId != expectedTenantId) return false;
            if (!long.TryParse(expiryPart, out var expiry) || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiry) return false;

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private string Sign(string payload)
    {
        var key = Encoding.UTF8.GetBytes(_config["Jwt:SigningKey"]!);
        using var hmac = new HMACSHA256(key);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static bool FixedTimeEquals(string a, string b)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
