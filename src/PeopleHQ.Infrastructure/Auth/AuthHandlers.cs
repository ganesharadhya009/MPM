using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Auth;
using PeopleHQ.Application.Auth.Interfaces;
using PeopleHQ.Domain.Identity;
using PeopleHQ.Domain.Leave;
using PeopleHQ.Domain.OrgStructure;
using PeopleHQ.Domain.Tenancy;
using PeopleHQ.Infrastructure.Persistence;
using PeopleHQ.Infrastructure.Persistence.Seed;

namespace PeopleHQ.Infrastructure.Auth;

/// <summary>Shared by Login/Refresh — resolves a user's effective permission keys via UserRole -> Role -> RolePermission -> Permission.</summary>
internal static class PermissionResolver
{
    public static async Task<IReadOnlyList<string>> GetPermissionKeysAsync(AppDbContext db, Guid userId, CancellationToken ct)
    {
        return await (
            from ur in db.UserRoles
            where ur.UserId == userId
            join rp in db.RolePermissions on ur.RoleId equals rp.RoleId
            join p in db.Permissions on rp.PermissionId equals p.Id
            select p.Key
        ).Distinct().ToListAsync(ct);
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResult>
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly IJwtTokenService _jwt;

    public LoginCommandHandler(AppDbContext db, UserManager<AppUser> userManager, IJwtTokenService jwt)
    {
        _db = db; _userManager = userManager; _jwt = jwt;
    }

    public async Task<AuthResult> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return new AuthResult(false, null, null, "Invalid email or password.");

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

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResult>
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly IJwtTokenService _jwt;

    public RefreshTokenCommandHandler(AppDbContext db, UserManager<AppUser> userManager, IJwtTokenService jwt)
    {
        _db = db; _userManager = userManager; _jwt = jwt;
    }

    public async Task<AuthResult> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == request.RefreshToken, ct);
        if (existing is null || !existing.IsActive)
            return new AuthResult(false, null, null, "Invalid or expired refresh token.");

        var user = await _userManager.FindByIdAsync(existing.UserId.ToString());
        if (user is null) return new AuthResult(false, null, null, "User no longer exists.");

        // Rotate: revoke the old token, issue a new one.
        existing.RevokedAtUtc = DateTime.UtcNow;
        var newRefreshToken = _jwt.GenerateRefreshToken(user.Id);
        existing.ReplacedByToken = newRefreshToken.Token;
        _db.RefreshTokens.Add(newRefreshToken);

        var permissionKeys = await PermissionResolver.GetPermissionKeysAsync(_db, user.Id, ct);
        var accessToken = _jwt.GenerateAccessToken(user, permissionKeys);

        await _db.SaveChangesAsync(ct);
        return new AuthResult(true, accessToken, newRefreshToken.Token, null);
    }
}

public class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly AppDbContext _db;
    public LogoutCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(LogoutCommand request, CancellationToken ct)
    {
        var tokens = await _db.RefreshTokens.Where(t => t.UserId == request.UserId && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var t in tokens) t.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class SignupCommandHandler : IRequestHandler<SignupCommand, SignupResult>
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public SignupCommandHandler(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db; _userManager = userManager;
    }

    public async Task<SignupResult> Handle(SignupCommand request, CancellationToken ct)
    {
        var subdomainTaken = await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Subdomain == request.Subdomain, ct);
        if (subdomainTaken) return new SignupResult(false, null, "Subdomain already in use.");

        var starterPlan = await _db.Plans.FirstOrDefaultAsync(p => p.Name == "Starter", ct);
        if (starterPlan is null)
        {
            starterPlan = new Plan { Name = "Starter", SeatLimit = 25, Price = 0, FeaturesJson = "{}" };
            _db.Plans.Add(starterPlan);
        }

        var tenant = new Tenant { Name = request.OrgName, Subdomain = request.Subdomain, PlanId = starterPlan.Id, TimeZone = "UTC" };
        _db.Tenants.Add(tenant);

        var adminUser = new AppUser
        {
            TenantId = tenant.Id,
            UserName = request.AdminEmail,
            Email = request.AdminEmail,
            Status = UserStatus.Active,
        };
        var createResult = await _userManager.CreateAsync(adminUser, request.AdminPassword);
        if (!createResult.Succeeded)
            return new SignupResult(false, null, string.Join("; ", createResult.Errors.Select(e => e.Description)));

        await SystemRoleSeeder.SeedForTenantAsync(_db, tenant.Id, ct);
        var adminRole = await _db.Roles.IgnoreQueryFilters().SingleAsync(r => r.TenantId == tenant.Id && r.Name == "TenantAdmin", ct);
        _db.UserRoles.Add(new UserRole { UserId = adminUser.Id, RoleId = adminRole.Id });

        // Seed defaults per 01-modules-functional-spec.md §A.
        _db.Locations.Add(new Location { TenantId = tenant.Id, Name = "Head Office", TimeZone = "UTC" });
        _db.Departments.Add(new Department { TenantId = tenant.Id, Name = "General" });
        _db.Designations.Add(new Designation { TenantId = tenant.Id, Title = "Employee" });
        foreach (var name in new[] { "Casual", "Sick", "Earned" })
            _db.LeaveTypes.Add(new LeaveType { TenantId = tenant.Id, Name = name, AccrualType = LeaveAccrualType.Fixed, AnnualEntitlement = 12 });

        await _db.SaveChangesAsync(ct);
        return new SignupResult(true, tenant.Id, null);
    }
}

public class EnableMfaCommandHandler : IRequestHandler<EnableMfaCommand, EnableMfaResult>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ITotpService _totp;

    public EnableMfaCommandHandler(UserManager<AppUser> userManager, ITotpService totp)
    {
        _userManager = userManager; _totp = totp;
    }

    public async Task<EnableMfaResult> Handle(EnableMfaCommand request, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString())
            ?? throw new Application.Common.Exceptions.NotFoundException(nameof(AppUser), request.UserId);

        var secret = _totp.GenerateSecret();
        var uri = _totp.BuildOtpAuthUri(secret, user.Email ?? string.Empty, "PeopleHQ");
        // Not persisted yet — VerifyMfaCommand persists MfaSecretEncrypted only after the first successful code,
        // so an abandoned setup never leaves MFA half-on.
        return new EnableMfaResult(secret, uri);
    }
}

public class VerifyMfaCommandHandler : IRequestHandler<VerifyMfaCommand, bool>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ITotpService _totp;

    public VerifyMfaCommandHandler(UserManager<AppUser> userManager, ITotpService totp)
    {
        _userManager = userManager; _totp = totp;
    }

    public async Task<bool> Handle(VerifyMfaCommand request, CancellationToken ct)
    {
        if (!_totp.ValidateCode(request.Secret, request.Code)) return false;

        var user = await _userManager.FindByIdAsync(request.UserId.ToString())
            ?? throw new Application.Common.Exceptions.NotFoundException(nameof(AppUser), request.UserId);

        user.MfaEnabled = true;
        user.MfaSecretEncrypted = request.Secret; // TODO(Phase 4 hardening): encrypt at rest via Key Vault-backed DataProtection, not plaintext.
        await _userManager.UpdateAsync(user);
        return true;
    }
}

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand>
{
    private readonly UserManager<AppUser> _userManager;
    public ForgotPasswordCommandHandler(UserManager<AppUser> userManager) => _userManager = userManager;

    public async Task Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null) return; // never reveal whether an email exists

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        // Phase 0: token handed back via the Notifications module once it exists; for now this is the
        // integration point a later task wires an email send onto (Notifications, Phase 1).
        _ = token;
    }
}

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, bool>
{
    private readonly UserManager<AppUser> _userManager;
    public ResetPasswordCommandHandler(UserManager<AppUser> userManager) => _userManager = userManager;

    public async Task<bool> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null) return false;

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        return result.Succeeded;
    }
}
