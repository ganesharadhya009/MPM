using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.Dashboards;
using PeopleHQ.Domain.Dashboards;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Dashboards;

/// <summary>Resolves the caller's "primary role" for dashboard purposes: the first Role (by name) among
/// the roles assigned to their user — see DashboardContracts for the documented v1 simplification for
/// multi-role users.</summary>
internal static class PrimaryRoleResolver
{
    public static async Task<string> ResolveAsync(AppDbContext db, ICurrentUserService currentUser, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("No authenticated user.");
        var roleName = await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
            .OrderBy(name => name)
            .FirstOrDefaultAsync(ct);
        return roleName ?? throw new ForbiddenException("You have no role assigned.");
    }
}

public class GetMyDashboardLayoutQueryHandler : IRequestHandler<GetMyDashboardLayoutQuery, DashboardLayoutDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public GetMyDashboardLayoutQueryHandler(AppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task<DashboardLayoutDto> Handle(GetMyDashboardLayoutQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var roleName = await PrimaryRoleResolver.ResolveAsync(_db, _currentUser, ct);

        var personal = await _db.DashboardLayouts.FirstOrDefaultAsync(d => d.RoleName == roleName && d.UserId == userId, ct);
        if (personal is not null) return new DashboardLayoutDto(roleName, personal.LayoutJson, true);

        var roleDefault = await _db.DashboardLayouts.FirstOrDefaultAsync(d => d.RoleName == roleName && d.UserId == null, ct);
        return new DashboardLayoutDto(roleName, roleDefault?.LayoutJson ?? "[]", false);
    }
}

public class SetMyDashboardLayoutCommandHandler : IRequestHandler<SetMyDashboardLayoutCommand>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUserService _currentUser;
    public SetMyDashboardLayoutCommandHandler(AppDbContext db, ITenantContext tenant, ICurrentUserService currentUser)
    { _db = db; _tenant = tenant; _currentUser = currentUser; }

    public async Task Handle(SetMyDashboardLayoutCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId!.Value;
        var roleName = await PrimaryRoleResolver.ResolveAsync(_db, _currentUser, ct);

        var personal = await _db.DashboardLayouts.FirstOrDefaultAsync(d => d.RoleName == roleName && d.UserId == userId, ct);
        if (personal is null)
        {
            _db.DashboardLayouts.Add(new DashboardLayout { TenantId = _tenant.TenantId, RoleName = roleName, UserId = userId, LayoutJson = request.LayoutJson });
        }
        else
        {
            personal.LayoutJson = request.LayoutJson;
        }
        await _db.SaveChangesAsync(ct);
    }
}

public class SetRoleDashboardDefaultCommandHandler : IRequestHandler<SetRoleDashboardDefaultCommand>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public SetRoleDashboardDefaultCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task Handle(SetRoleDashboardDefaultCommand request, CancellationToken ct)
    {
        var existing = await _db.DashboardLayouts.FirstOrDefaultAsync(d => d.RoleName == request.RoleName && d.UserId == null, ct);
        if (existing is null)
        {
            _db.DashboardLayouts.Add(new DashboardLayout { TenantId = _tenant.TenantId, RoleName = request.RoleName, UserId = null, LayoutJson = request.LayoutJson });
        }
        else
        {
            existing.LayoutJson = request.LayoutJson;
        }
        await _db.SaveChangesAsync(ct);
    }
}

public class GetRoleDashboardDefaultsQueryHandler : IRequestHandler<GetRoleDashboardDefaultsQuery, IReadOnlyList<DashboardLayoutDto>>
{
    private readonly AppDbContext _db;
    public GetRoleDashboardDefaultsQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<DashboardLayoutDto>> Handle(GetRoleDashboardDefaultsQuery request, CancellationToken ct)
        => await _db.DashboardLayouts.Where(d => d.UserId == null)
            .Select(d => new DashboardLayoutDto(d.RoleName, d.LayoutJson, false))
            .ToListAsync(ct);
}
