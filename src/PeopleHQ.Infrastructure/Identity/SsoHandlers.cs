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
        var state = _stateSigner.Create(_tenant.TenantId, TimeSpan.FromMinutes(10));
        var redirectUri = BuildRedirectUri(_httpContextAccessor);

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
        if (!_stateSigner.TryValidate(request.State, _tenant.TenantId))
            return new AuthResult(false, null, null, "Invalid or expired SSO login attempt.");

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

        var email = principal.FindFirst("email")?.Value ?? principal.FindFirst("preferred_username")?.Value;
        if (string.IsNullOrWhiteSpace(email))
            return new AuthResult(false, null, null, "The identity provider did not return an email address.");

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            // JIT provisioning: default to the least-privileged Employee role — a TenantAdmin can promote
            // afterward via existing role management. Documented v1 simplification, not a tracked defect.
            user = new AppUser { TenantId = _tenant.TenantId, UserName = email, Email = email, Status = UserStatus.Active };
            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
                return new AuthResult(false, null, null, string.Join("; ", createResult.Errors.Select(e => e.Description)));

            var employeeRole = await _db.Roles.FirstOrDefaultAsync(r => r.TenantId == _tenant.TenantId && r.Name == "Employee", ct);
            if (employeeRole is not null) _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = employeeRole.Id });
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
