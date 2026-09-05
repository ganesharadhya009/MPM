using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace PeopleHQ.Infrastructure.Identity;

public record OidcDiscoveryDocument(string AuthorizationEndpoint, string TokenEndpoint, string JwksUri, string Issuer);

/// <summary>
/// Minimal OIDC relying-party client: discovery document fetch, authorization-code-for-token exchange, and
/// id_token signature/issuer/audience/expiry validation against the provider's published JWKS. No new
/// NuGet package — built on System.IdentityModel.Tokens.Jwt / Microsoft.IdentityModel.Tokens, both already
/// referenced (JwtTokenService uses the latter for PeopleHQ's own token signing).
/// </summary>
public interface IOidcClient
{
    Task<OidcDiscoveryDocument> GetDiscoveryDocumentAsync(string authority, CancellationToken ct);
    Task<string> ExchangeCodeForIdTokenAsync(OidcDiscoveryDocument discovery, string clientId, string clientSecret, string code, string redirectUri, CancellationToken ct);
    Task<ClaimsPrincipal> ValidateIdTokenAsync(OidcDiscoveryDocument discovery, string idToken, string clientId, CancellationToken ct);
}

public class OidcClient : IOidcClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    public OidcClient(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public async Task<OidcDiscoveryDocument> GetDiscoveryDocumentAsync(string authority, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(nameof(OidcClient));
        var json = await client.GetStringAsync($"{authority.TrimEnd('/')}/.well-known/openid-configuration", ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new OidcDiscoveryDocument(
            root.GetProperty("authorization_endpoint").GetString()!,
            root.GetProperty("token_endpoint").GetString()!,
            root.GetProperty("jwks_uri").GetString()!,
            root.GetProperty("issuer").GetString()!);
    }

    public async Task<string> ExchangeCodeForIdTokenAsync(OidcDiscoveryDocument discovery, string clientId, string clientSecret, string code, string redirectUri, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(nameof(OidcClient));
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        };
        using var response = await client.PostAsync(discovery.TokenEndpoint, new FormUrlEncodedContent(form), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OIDC token exchange failed ({(int)response.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id_token").GetString()
            ?? throw new InvalidOperationException("OIDC token response did not contain an id_token.");
    }

    public async Task<ClaimsPrincipal> ValidateIdTokenAsync(OidcDiscoveryDocument discovery, string idToken, string clientId, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(nameof(OidcClient));
        var jwksJson = await client.GetStringAsync(discovery.JwksUri, ct);
        var jwks = new JsonWebKeySet(jwksJson);

        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = discovery.Issuer,
            ValidAudience = clientId,
            IssuerSigningKeys = jwks.Keys,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(idToken, validationParameters, out _);
        return principal;
    }
}
