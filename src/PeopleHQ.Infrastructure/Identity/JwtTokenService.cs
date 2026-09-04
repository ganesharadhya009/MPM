using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PeopleHQ.Application.Auth.Interfaces;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Infrastructure.Identity;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config) => _config = config;

    public string GenerateAccessToken(AppUser user, IReadOnlyList<string> permissionKeys)
    {
        var signingKey = _config["Jwt:SigningKey"]!;
        var issuer = _config["Jwt:Issuer"]!;
        var minutes = int.Parse(_config["Jwt:AccessTokenMinutes"] ?? "15");

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("tenant_id", user.TenantId.ToString()),
            new("email", user.Email ?? string.Empty),
        };
        claims.AddRange(permissionKeys.Select(p => new Claim("permission", p)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(minutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public RefreshToken GenerateRefreshToken(Guid userId)
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return new RefreshToken
        {
            UserId = userId,
            Token = Convert.ToBase64String(bytes),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
        };
    }
}
