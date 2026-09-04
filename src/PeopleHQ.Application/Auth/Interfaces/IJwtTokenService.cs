using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Application.Auth.Interfaces;

public interface IJwtTokenService
{
    string GenerateAccessToken(AppUser user, IReadOnlyList<string> permissionKeys);
    RefreshToken GenerateRefreshToken(Guid userId);
}

public interface ITotpService
{
    string GenerateSecret();
    string BuildOtpAuthUri(string secret, string email, string issuer);
    bool ValidateCode(string secret, string code);
}
