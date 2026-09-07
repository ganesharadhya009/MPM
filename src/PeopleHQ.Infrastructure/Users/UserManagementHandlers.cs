using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.Users;
using PeopleHQ.Domain.Identity;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Users;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, PagedResult<UserSummaryDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public GetUsersQueryHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<PagedResult<UserSummaryDto>> Handle(GetUsersQuery request, CancellationToken ct)
    {
        // AppUser is deliberately NOT ITenantOwned (ASP.NET Identity's own UserManager looks users up by
        // email across the whole table) — every query here filters explicitly.
        var query = _db.Users.Where(u => u.TenantId == _tenant.TenantId).OrderBy(u => u.Email);
        var total = await query.CountAsync(ct);
        var users = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);

        var userIds = users.Select(u => u.Id).ToList();
        var roleNamesByUser = (await _db.UserRoles
                .Where(ur => userIds.Contains(ur.UserId))
                .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name }) // Roles carries its own tenant filter
                .ToListAsync(ct))
            .ToLookup(x => x.UserId, x => x.Name);

        var dtos = users.Select(u => new UserSummaryDto(u.Id, u.Email, u.Status, u.MfaEnabled, u.LastLoginAtUtc, roleNamesByUser[u.Id].ToList())).ToList();
        return PagedResult<UserSummaryDto>.Create(dtos, request.Page, request.PageSize, total);
    }
}

public class GetRolesQueryHandler : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    private readonly AppDbContext _db;
    public GetRolesQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<RoleDto>> Handle(GetRolesQuery request, CancellationToken ct)
        => await _db.Roles.OrderBy(r => r.Name).Select(r => new RoleDto(r.Id, r.Name, r.IsSystem)).ToListAsync(ct);
}

public class AssignRoleToUserCommandHandler : IRequestHandler<AssignRoleToUserCommand>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public AssignRoleToUserCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task Handle(AssignRoleToUserCommand request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.TenantId == _tenant.TenantId, ct)
            ?? throw new NotFoundException(nameof(AppUser), request.UserId);
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId && r.TenantId == _tenant.TenantId, ct)
            ?? throw new NotFoundException(nameof(Role), request.RoleId);

        var alreadyAssigned = await _db.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id, ct);
        if (!alreadyAssigned) _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });

        await _db.SaveChangesAsync(ct);
    }
}

public class RemoveRoleFromUserCommandHandler : IRequestHandler<RemoveRoleFromUserCommand>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public RemoveRoleFromUserCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task Handle(RemoveRoleFromUserCommand request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.TenantId == _tenant.TenantId, ct)
            ?? throw new NotFoundException(nameof(AppUser), request.UserId);
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId && r.TenantId == _tenant.TenantId, ct)
            ?? throw new NotFoundException(nameof(Role), request.RoleId);

        var link = await _db.UserRoles.FirstOrDefaultAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id, ct);
        if (link is not null) _db.UserRoles.Remove(link);

        await _db.SaveChangesAsync(ct);
    }
}

public class SetUserStatusCommandHandler : IRequestHandler<SetUserStatusCommand>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public SetUserStatusCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task Handle(SetUserStatusCommand request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.TenantId == _tenant.TenantId, ct)
            ?? throw new NotFoundException(nameof(AppUser), request.UserId);
        user.Status = request.Status;
        await _db.SaveChangesAsync(ct);
    }
}

public class BulkAssignRoleCommandHandler : IRequestHandler<BulkAssignRoleCommand, BulkAssignRoleResult>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public BulkAssignRoleCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<BulkAssignRoleResult> Handle(BulkAssignRoleCommand request, CancellationToken ct)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId && r.TenantId == _tenant.TenantId, ct)
            ?? throw new NotFoundException(nameof(Role), request.RoleId);

        var failures = new List<BulkAssignRoleFailure>();
        var succeeded = 0;

        foreach (var userId in request.UserIds)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == _tenant.TenantId, ct);
            if (user is null)
            {
                failures.Add(new BulkAssignRoleFailure(userId, "User not found in this tenant."));
                continue;
            }

            var alreadyAssigned = await _db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == role.Id, ct);
            if (!alreadyAssigned) _db.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id });
            succeeded++;
        }

        await _db.SaveChangesAsync(ct);
        return new BulkAssignRoleResult(request.UserIds.Count, succeeded, failures);
    }
}
