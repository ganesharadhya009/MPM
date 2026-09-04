namespace PeopleHQ.Application.Common.Interfaces;

/// <summary>Lets a handler enforce object-level authorization beyond the endpoint's [RequirePermission] gate —
/// e.g. a query that accepts an arbitrary employeeId must still confirm the caller may see THAT employee's data,
/// not just that they hold the permission key at all (a plain Employee role holds leave.read for their own
/// self-service view, but must not be able to pass another employee's id and read their leave history).</summary>
public interface IPermissionChecker
{
    bool HasPermission(string permissionKey);
}
