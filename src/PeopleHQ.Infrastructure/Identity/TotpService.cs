using OtpNet;
using PeopleHQ.Application.Auth.Interfaces;

namespace PeopleHQ.Infrastructure.Identity;

public class TotpService : ITotpService
{
    public string GenerateSecret() => Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));

    public string BuildOtpAuthUri(string secret, string email, string issuer) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}";

    public bool ValidateCode(string secret, string code)
    {
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        return totp.VerifyTotp(code, out _, new VerificationWindow(1, 1));
    }
}
