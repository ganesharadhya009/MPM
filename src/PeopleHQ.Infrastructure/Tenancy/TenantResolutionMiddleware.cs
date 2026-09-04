using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Tenancy;

/// <summary>
/// Resolves the tenant from the subdomain before the request reaches
/// controllers, stamping it into ITenantContext for EF Core's global query
/// filter to read (00-overview.md §4). Cross-checks against the JWT's
/// tenant_id claim when present — mismatch is a 403 (defense in depth
/// against a stolen/misused token), never a silent fall-through.
/// A "X-Tenant" header is accepted for direct API/integration use where
/// there's no browser subdomain (03-api-design.md).
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AppDbContext db, TenantContext tenantContext)
    {
        // Public, unauthenticated endpoints (signup, health check, swagger) don't require a resolved tenant.
        if (IsPublicPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var subdomain = ResolveSubdomainCandidate(context);
        if (string.IsNullOrWhiteSpace(subdomain))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var tenant = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Subdomain == subdomain);
        if (tenant is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var tenantClaim = context.User?.FindFirst("tenant_id")?.Value;
        if (tenantClaim is not null && Guid.TryParse(tenantClaim, out var claimedTenantId) && claimedTenantId != tenant.Id)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        tenantContext.SetTenant(tenant.Id);
        await _next(context);
    }

    private static bool IsPublicPath(PathString path) =>
        path.StartsWithSegments("/api/v1/auth/signup") ||
        path.StartsWithSegments("/healthz") ||
        path.StartsWithSegments("/swagger");

    private static string? ResolveSubdomainCandidate(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Tenant", out var headerValue) && !string.IsNullOrWhiteSpace(headerValue))
            return headerValue.ToString();

        var host = context.Request.Host.Host;
        var parts = host.Split('.');
        return parts.Length > 2 ? parts[0] : null; // acme.peoplehq.app -> "acme"; localhost/bare host -> no subdomain
    }
}
