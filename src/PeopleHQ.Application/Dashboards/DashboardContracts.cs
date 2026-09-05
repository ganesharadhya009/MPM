using MediatR;

namespace PeopleHQ.Application.Dashboards;

// Configurable dashboards per role ("most needed options" #8, 01-modules-functional-spec.md). Resolution
// order for "my" dashboard: 1) a personal DashboardLayout row for (RoleName=my primary role, UserId=me),
// 2) the tenant's role-level default (UserId=null) for that role, 3) an empty layout if neither exists.
// v1 simplification: a user with multiple assigned roles uses the first UserRole row found as their
// "primary role" for dashboard purposes — documented, not a tracked defect.

public record GetMyDashboardLayoutQuery : IRequest<DashboardLayoutDto>;
public record DashboardLayoutDto(string RoleName, string LayoutJson, bool IsPersonalized);
public record SetMyDashboardLayoutCommand(string LayoutJson) : IRequest;

public record SetRoleDashboardDefaultCommand(string RoleName, string LayoutJson) : IRequest;
public record GetRoleDashboardDefaultsQuery : IRequest<IReadOnlyList<DashboardLayoutDto>>;
