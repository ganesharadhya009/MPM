namespace PeopleHQ.Application.Common.Interfaces;

/// <summary>
/// Scoped per-request. Resolved by TenantResolutionMiddleware from the
/// subdomain, cross-checked against the JWT tenant_id claim (00-overview.md §4).
/// EF Core's global query filter reads TenantId from this on every query.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    bool HasTenant { get; }
    void SetTenant(Guid tenantId);
}

public interface ICurrentUserService
{
    Guid? UserId { get; }
}
