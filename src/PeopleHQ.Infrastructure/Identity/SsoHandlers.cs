using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Auth;
using PeopleHQ.Application.Auth.Interfaces;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Domain.Identity;
using PeopleHQ.Infrastructure.Auth;
using PeopleHQ.Infrastructure.Integrations;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Identity;

public class UpsertSsoConfigurationCommandHandler : IRequestHandler<UpsertSsoConfigurationCommand>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public UpsertSsoConfigurationCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task Handle(UpsertSsoConfigurationCommand request, CancellationToken ct)
    {
        if (!SsrfGuard.IsAllowedTargetUrl(request.Authority, out var uri))
            throw new ValidationException(nameof(request.Authority), "Authority must be an absolute https:// URL with no embedded credentials.");
        if (!await SsrfGuard.ResolvesToPublicAddressAsync(uri!.Host, ct))
            throw new ValidationException(nameof(request.Authority), "Authority must resolve to a public address.");

        var existing = await _db.SsoConfigurations.FirstOrDefaultAsync(c => c.TenantId == _tenant.TenantId, ct);
        if (existing is null)
        {
            _db.SsoConfigurations.Add(new SsoConfiguration
            {
                TenantId = _tenant.TenantId,
                ClientId = request.ClientId,
                ClientSecret = request.ClientSecret,
                Authority = request.Authority,
                IsEnabled = request.IsEnabled
            });
        }
        else
        {
            existing.ClientId = request.ClientId;
            existing.ClientSecret = request.ClientSecret;
            existing.Authority = request.Authority;
            existing.IsEnabled = request.IsEnabled;
        }
        await _db.SaveChangesAsync(ct);
    }
}

public class GetSsoConfigurationQueryHandler : IRequestHandler<GetSsoConfigurationQuery, SsoConfigurationDto?>
{
    private readonly AppDbContext _db;
    public GetSsoConfigurationQueryHandler(AppDbContext db) => _db = db;

    public async Task<SsoConfigurationDto?> Handle(GetSsoConfigurationQuery request, CancellationToken ct)
    {
        var config = await _db.SsoConfigurations.FirstOrDefaultAsync(ct);
        return config is null ? null : new SsoConfigurationDto(config.ClientId, config.Authority, config.IsEnabled);
    }
}

public class InitiateSsoLoginCommandHandler : IRequestHandler<InitiateSsoLoginCommand, string>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IOidcClient _oidc;
    private readonly SsoStateSigner _stateSigner;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public InitiateSsoLoginCommandHandler(AppDbContext db, ITenantContext tenant, IOidcClient oidc, SsoStateSigner stateSigner, IHttpContextAccessor httpContextAccessor)
    { _db = db; _tenant = tenant; _oidc = oidc; _stateSigner = stateSigner; _httpContextAccessor = httpContextAccessor; }

    public async Task<string> Handle(InitiateSsoLoginCommand request, CancellationToken ct)
    {
        var config = await _db.SsoConfigurations.FirstOrDefaultAsync(c => c.IsEnabled, ct)
            ?? throw new NotFoundException(nameof(SsoConfiguration), _tenant.TenantId);

        var discovery = await _oidc.GetDiscoveryDocumentAsync(config.Authority, ct);

        // Login-CSRF defense: the nonce embedded in `state` is also stashed in an HttpOnly cookie on THIS
        // browser. CompleteSsoLoginCommandHandler requires the callback's state to embed the same nonce as
        // whatever cookie arrives with it — an attacker who completes their own OIDC flow and tricks a
        // victim into hitting the callback with the attacker's code+state won't have the victim's cookie,
        // so the victim's session can never end up authenticated as the attacker.
        var nonce = SsoStateSigner.GenerateNonce();
        var state = _stateSigner.Create(_tenant.TenantId, nonce, TimeSpan.FromMinutes(10));
        var redirectUri = BuildRedirectUri(_httpContextAccessor);

        _httpContextAccessor.HttpContext?.Response.Cookies.Append("sso_state_nonce", nonce, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax, // Lax, not Strict — the callback arrives via a top-level cross-site redirect from the IdP
            Expires = DateTimeOffset.UtcNow.AddMinutes(10)
        });

        var query = string.Join('&', new[]
        {
            "response_type=code",
            $"client_id={Uri.EscapeDataString(config.ClientId)}",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            "scope=" + Uri.EscapeDataString("openid email profile"),
            $"state={Uri.EscapeDataString(state)}"
        });
        return $"{discovery.AuthorizationEndpoint}?{query}";
    }

    internal static string BuildRedirectUri(IHttpContextAccessor accessor)
    {
        var request = accessor.HttpContext?.Request ?? throw new InvalidOperationException("No active HTTP request.");
        return $"{request.Scheme}://{request.Host}/api/v1/auth/sso/callback";
    }
}

public class CompleteSsoLoginCommandHandler : IRequestHandler<CompleteSsoLoginCommand, AuthResult>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IOidcClient _oidc;
    private readonly SsoStateSigner _stateSigner;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<AppUser> _userManager;
    private readonly IJwtTokenService _jwt;

    public CompleteSsoLoginCommandHandler(AppDbContext db, ITenantContext tenant, IOidcClient oidc, SsoStateSigner stateSigner,
        IHttpContextAccessor httpContextAccessor, UserManager<AppUser> userManager, IJwtTokenService jwt)
    { _db = db; _tenant = tenant; _oidc = oidc; _stateSigner = stateSigner; _httpContextAccessor = httpContextAccessor; _userManager = userManager; _jwt = jwt; }

    public async Task<AuthResult> Handle(CompleteSsoLoginCommand request, CancellationToken ct)
    {
        // Login-CSRF defense (paired with InitiateSsoLoginCommandHandler's cookie): the callback's state
        // must embed the same nonce as the cookie THIS browser was given when the flow started. The cookie
        // is single-use — deleted here regardless of outcome.
        var httpContext = _httpContextAccessor.HttpContext;
        var cookieNonce = httpContext?.Request.Cookies["sso_state_nonce"];
        httpContext?.Response.Cookies.Delete("sso_state_nonce");

        if (!_stateSigner.TryValidate(request.State, _tenant.TenantId, out var nonceFromState))
            return new AuthResult(false, null, null, "Invalid or expired SSO login attempt.");
        if (cookieNonce is null || nonceFromState is null ||
            !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(cookieNonce), System.Text.Encoding.UTF8.GetBytes(nonceFromState)))
            return new AuthResult(false, null, null, "This SSO login attempt does not match your browser session.");

        var config = await _db.SsoConfigurations.FirstOrDefaultAsync(c => c.IsEnabled, ct);
        if (config is null) return new AuthResult(false, null, null, "SSO is not enabled for this tenant.");

        var discovery = await _oidc.GetDiscoveryDocumentAsync(config.Authority, ct);
        var redirectUri = InitiateSsoLoginCommandHandler.BuildRedirectUri(_httpContextAccessor);

        string idToken;
        System.Security.Claims.ClaimsPrincipal principal;
        try
        {
            idToken = await _oidc.ExchangeCodeForIdTokenAsync(discovery, config.ClientId, config.ClientSecret, request.Code, redirectUri, ct);
            principal = await _oidc.ValidateIdTokenAsync(discovery, idToken, config.ClientId, ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Microsoft.IdentityModel.Tokens.SecurityTokenException)
        {
            return new AuthResult(false, null, null, "SSO sign-in failed: could not verify the identity provider's response.");
        }

        var subject = principal.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject))
            return new AuthResult(false, null, null, "The identity provider did not return a subject identifier.");

        // Namespaced by issuer so two different tenants' IdPs can never collide on the same ProviderKey.
        var loginProvider = $"oidc:{discovery.Issuer}";

        // Prefer the already-linked account (safe regardless of email verification — the link itself was
        // only ever created below, after a verified-email match or fresh JIT provisioning).
        var user = await _userManager.FindByLoginAsync(loginProvider, subject);
        if (user is not null && user.TenantId != _tenant.TenantId) user = null; // defensive: AppUser has no tenant query filter

        if (user is null)
        {
            // No existing link: an email claim can only be trusted to identify/link an account when the
            // IdP has verified it — otherwise anyone could claim victim@company.com at a permissive IdP and
            // either take over that person's existing PeopleHQ account or impersonate them via a fresh one.
            // preferred_username is deliberately NOT used as a fallback — it is not guaranteed to be an
            // email or to be verified.
            var emailVerified = string.Equals(principal.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            var email = principal.FindFirst("email")?.Value;
            if (!emailVerified || string.IsNullOrWhiteSpace(email))
                return new AuthResult(false, null, null, "Your identity provider must verify your email address before you can sign in via SSO.");

            var normalizedEmail = _userManager.NormalizeEmail(email);
            user = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == _tenant.TenantId && u.NormalizedEmail == normalizedEmail, ct);

            if (user is null)
            {
                // JIT provisioning: default to the least-privileged Employee role — a TenantAdmin can
                // promote afterward via existing role management. Documented v1 simplification.
                user = new AppUser { TenantId = _tenant.TenantId, UserName = email, Email = email, Status = UserStatus.Active };
                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                    return new AuthResult(false, null, null, string.Join("; ", createResult.Errors.Select(e => e.Description)));

                var employeeRole = await _db.Roles.FirstOrDefaultAsync(r => r.TenantId == _tenant.TenantId && r.Name == "Employee", ct);
                if (employeeRole is not null) _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = employeeRole.Id });
            }

            // Persist the link so every subsequent login uses the sub-based path above, never re-touching
            // the email-matching path for this account.
            var linkResult = await _userManager.AddLoginAsync(user, new UserLoginInfo(loginProvider, subject, discovery.Issuer));
            if (!linkResult.Succeeded)
                return new AuthResult(false, null, null, string.Join("; ", linkResult.Errors.Select(e => e.Description)));
        }

        if (user.Status == UserStatus.Disabled)
            return new AuthResult(false, null, null, "This account has been disabled.");

        var permissionKeys = await PermissionResolver.GetPermissionKeysAsync(_db, user.Id, ct);
        var accessToken = _jwt.GenerateAccessToken(user, permissionKeys);
        var refreshToken = _jwt.GenerateRefreshToken(user.Id);
        _db.RefreshTokens.Add(refreshToken);

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new AuthResult(true, accessToken, refreshToken.Token, null);
    }
}
