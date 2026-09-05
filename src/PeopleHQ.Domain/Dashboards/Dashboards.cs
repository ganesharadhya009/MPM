using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.Dashboards;

/// <summary>
/// "Most needed options" #8 (01-modules-functional-spec.md) — configurable dashboards per role rather than
/// one fixed layout. A row with UserId == null is the tenant's default layout for RoleName; a row with
/// UserId set is that specific user's personal override. Uniqueness of (RoleName, UserId) is enforced at
/// the application layer (find-then-upsert) rather than a DB constraint, since Postgres treats multiple
/// NULLs in a unique index as distinct — see DashboardHandlers.
/// </summary>
public class DashboardLayout : TenantOwnedEntity
{
    public string RoleName { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string LayoutJson { get; set; } = "[]";
}
