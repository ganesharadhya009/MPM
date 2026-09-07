using MediatR;
using PeopleHQ.Application.Common;
using PeopleHQ.Domain.Identity;

namespace PeopleHQ.Application.Users;

// User & Role management (§M Admin/Settings "Users & Roles") plus bulk role assignment ("most needed
// options" #6). TenantAdmin-only (UserManage/RoleManage). AppUser and UserRole are NOT ITenantOwned (no
// automatic global query filter — AppUser deliberately so, since ASP.NET Identity's UserManager looks users
// up by email across the whole Users table), so every handler here must explicitly filter by TenantId.
//
// v1 simplification: no user-invitation email flow yet (UserInvite permission exists but the actual invite
// command is a documented follow-up — creating an AppUser with a real onboarding email is a distinct,
// larger feature). This module covers listing, role assignment (single + bulk), and enable/disable only.

public record GetUsersQuery(int Page = 1, int PageSize = 25) : IRequest<PagedResult<UserSummaryDto>>;
public record UserSummaryDto(Guid Id, string? Email, UserStatus Status, bool MfaEnabled, DateTime? LastLoginAtUtc, IReadOnlyList<string> RoleNames);

public record GetRolesQuery : IRequest<IReadOnlyList<RoleDto>>;
public record RoleDto(Guid Id, string Name, bool IsSystem);

public record AssignRoleToUserCommand(Guid UserId, Guid RoleId) : IRequest;
public record RemoveRoleFromUserCommand(Guid UserId, Guid RoleId) : IRequest;
public record SetUserStatusCommand(Guid UserId, UserStatus Status) : IRequest;

public record BulkAssignRoleCommand(IReadOnlyList<Guid> UserIds, Guid RoleId) : IRequest<BulkAssignRoleResult>;
public record BulkAssignRoleFailure(Guid UserId, string Error);
public record BulkAssignRoleResult(int TotalRequested, int Succeeded, IReadOnlyList<BulkAssignRoleFailure> Failures);
