using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.Tenancy;

public enum TenantStatus { Trial, Active, Suspended, Cancelled }

/// <summary>
/// Platform-level — does NOT carry TenantId itself (it IS the tenant).
/// 02-data-model-erd.md "tenants" table.
/// </summary>
public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;
    public Guid PlanId { get; set; }
    public Plan? Plan { get; set; }
    public TenantStatus Status { get; set; } = TenantStatus.Trial;
    public string TimeZone { get; set; } = "UTC";
    public string? Industry { get; set; }
    public string? LogoBlobUrl { get; set; }
    public bool EmailVerified { get; set; }
}

/// <summary>Platform-level. Feature flags resolved from here + optional per-tenant overrides (00-overview.md §4).</summary>
public class Plan : BaseEntity
{
    public string Name { get; set; } = string.Empty; // Starter / Growth / Enterprise
    public int SeatLimit { get; set; }
    public decimal Price { get; set; }
    public string FeaturesJson { get; set; } = "{}";
}
