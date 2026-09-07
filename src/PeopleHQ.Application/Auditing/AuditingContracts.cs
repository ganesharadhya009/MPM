using MediatR;
using PeopleHQ.Application.Common;
using PeopleHQ.Domain.Auditing;

namespace PeopleHQ.Application.Auditing;

// Audit log query + data export/deletion center ("most needed options" #7: "non-negotiable for any SaaS
// handling employee PII"). All TenantAdmin-only.

public record GetAuditLogQuery(string? EntityName = null, Guid? EntityId = null, Guid? ActorUserId = null,
    DateTime? StartDateUtc = null, DateTime? EndDateUtc = null, int Page = 1, int PageSize = 50) : IRequest<PagedResult<AuditLogEntryDto>>;
public record AuditLogEntryDto(Guid Id, Guid? ActorUserId, string EntityName, Guid EntityId, AuditAction Action, string DiffJson, DateTime CreatedAtUtc);

/// <summary>A data-subject-access-request-style export: one combined JSON document covering the employee's
/// core profile plus their documents, leave requests, attendance records, goals, feedback received, and
/// payslip metadata. Not every table in the schema — a representative, GDPR-DSAR-shaped subset; extending
/// coverage further is a documented follow-up, not a defect.</summary>
public record ExportEmployeeDataCommand(Guid EmployeeId) : IRequest<EmployeeDataExportDto>;
public record EmployeeDataExportDto(string ExportJson, DateTime ExportedAtUtc);

/// <summary>Right-to-erasure: redacts the employee's PII fields (name, personal/work email, phone, date of
/// birth) in place rather than hard-deleting the row. Other tables referencing EmployeeId (attendance,
/// leave, payroll, workflow history, etc.) are left untouched — a real cascading erasure across every
/// referencing table is a larger, higher-risk undertaking (some of that data, e.g. payroll records, may
/// carry its own statutory retention requirements) and is a documented follow-up, not built here.</summary>
public record AnonymizeEmployeeDataCommand(Guid EmployeeId) : IRequest;
