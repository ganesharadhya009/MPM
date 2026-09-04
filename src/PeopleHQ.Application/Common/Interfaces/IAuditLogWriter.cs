using PeopleHQ.Domain.Auditing;

namespace PeopleHQ.Application.Common.Interfaces;

public interface IAuditLogWriter
{
    Task WriteAsync(string entityName, Guid entityId, AuditAction action, object? before, object? after, CancellationToken ct = default);
}
