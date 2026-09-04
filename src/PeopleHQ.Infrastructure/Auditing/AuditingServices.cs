using System.Text.Json;
using Microsoft.AspNetCore.Http;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Domain.Auditing;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Auditing;

/// <summary>Append-only writer — no method here ever updates or deletes an AuditLogEntry (NFR-SEC-09).</summary>
public class AuditLogWriter : IAuditLogWriter
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantContext _tenantContext;

    public AuditLogWriter(AppDbContext db, ICurrentUserService currentUser, ITenantContext tenantContext)
    {
        _db = db;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task WriteAsync(string entityName, Guid entityId, AuditAction action, object? before, object? after, CancellationToken ct = default)
    {
        var diff = JsonSerializer.Serialize(new { before, after });
        _db.AuditLogs.Add(new AuditLogEntry
        {
            TenantId = _tenantContext.TenantId,
            ActorUserId = _currentUser.UserId,
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            DiffJson = diff,
        });
        await _db.SaveChangesAsync(ct);
    }
}

public class CurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; }

    public CurrentUserService(IHttpContextAccessor accessor)
    {
        var sub = accessor.HttpContext?.User?.FindFirst("sub")?.Value;
        UserId = sub is not null && Guid.TryParse(sub, out var id) ? id : null;
    }
}
