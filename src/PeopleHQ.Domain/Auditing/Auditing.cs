using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.Auditing;

public enum AuditAction { Create, Update, Delete, StatusChange }

/// <summary>Append-only at the application layer (NFR-SEC-09) — no update/delete code path may ever target this table.</summary>
public class AuditLogEntry : TenantOwnedEntity
{
    public Guid? ActorUserId { get; set; } // null = system action
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public AuditAction Action { get; set; }
    public string DiffJson { get; set; } = "{}"; // before/after
}
