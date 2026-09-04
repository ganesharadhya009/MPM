namespace PeopleHQ.Domain.Common;

/// <summary>
/// Base for every domain entity. Id + audit timestamps/actors per
/// 02-data-model-erd.md conventions (all tables, all tenant-owned or not).
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>Implemented by every tenant-owned entity — enforced by the EF Core global query filter.</summary>
public interface ITenantOwned
{
    Guid TenantId { get; set; }
}

/// <summary>Master-data tables soft-delete rather than hard-delete (02-data-model-erd.md).</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
}

/// <summary>Convenience base combining the common tenant-owned + soft-deletable shape used by most master data.</summary>
public abstract class TenantOwnedEntity : BaseEntity, ITenantOwned, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}

/// <summary>Money fields use decimal(14,2) + this currency code, never float/double (00-overview.md §6).</summary>
public static class Money
{
    public const string DefaultCurrency = "INR";
}
