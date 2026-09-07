using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Auditing;
using PeopleHQ.Application.Common;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Domain.Employees;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Auditing;

public class GetAuditLogQueryHandler : IRequestHandler<GetAuditLogQuery, PagedResult<AuditLogEntryDto>>
{
    private readonly AppDbContext _db;
    public GetAuditLogQueryHandler(AppDbContext db) => _db = db;

    public async Task<PagedResult<AuditLogEntryDto>> Handle(GetAuditLogQuery request, CancellationToken ct)
    {
        var query = _db.AuditLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.EntityName)) query = query.Where(a => a.EntityName == request.EntityName);
        if (request.EntityId is not null) query = query.Where(a => a.EntityId == request.EntityId);
        if (request.ActorUserId is not null) query = query.Where(a => a.ActorUserId == request.ActorUserId);
        if (request.StartDateUtc is not null) query = query.Where(a => a.CreatedAtUtc >= request.StartDateUtc);
        if (request.EndDateUtc is not null) query = query.Where(a => a.CreatedAtUtc <= request.EndDateUtc);
        query = query.OrderByDescending(a => a.CreatedAtUtc);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(a => new AuditLogEntryDto(a.Id, a.ActorUserId, a.EntityName, a.EntityId, a.Action, a.DiffJson, a.CreatedAtUtc))
            .ToListAsync(ct);
        return PagedResult<AuditLogEntryDto>.Create(items, request.Page, request.PageSize, total);
    }
}

public class ExportEmployeeDataCommandHandler : IRequestHandler<ExportEmployeeDataCommand, EmployeeDataExportDto>
{
    private readonly AppDbContext _db;
    public ExportEmployeeDataCommandHandler(AppDbContext db) => _db = db;

    public async Task<EmployeeDataExportDto> Handle(ExportEmployeeDataCommand request, CancellationToken ct)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, ct)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        var documents = await _db.EmployeeDocuments.Where(d => d.EmployeeId == request.EmployeeId)
            .Select(d => new { d.DocType, d.BlobUrl, d.UploadedAtUtc, d.ExpiresAt }).ToListAsync(ct);
        var leaveRequests = await _db.LeaveRequests.Where(r => r.EmployeeId == request.EmployeeId)
            .Select(r => new { r.LeaveTypeId, r.StartDate, r.EndDate, r.IsHalfDay, r.Reason, r.Status }).ToListAsync(ct);
        var attendanceRecords = await _db.AttendanceRecords.Where(r => r.EmployeeId == request.EmployeeId)
            .Select(r => new { r.Date, r.CheckInAtUtc, r.CheckOutAtUtc, r.Status, r.OvertimeHours }).ToListAsync(ct);
        var goals = await _db.Goals.Where(g => g.EmployeeId == request.EmployeeId)
            .Select(g => new { g.Title, g.Description, g.TargetDate, g.ProgressPercent, g.Status }).ToListAsync(ct);
        var feedbackReceived = await _db.FeedbackNotes.Where(f => f.ToEmployeeId == request.EmployeeId)
            .Select(f => new { f.Message, f.Visibility, f.CreatedAtUtc }).ToListAsync(ct);
        var payslips = await _db.Payslips.Where(p => p.EmployeeId == request.EmployeeId)
            .Select(p => new { p.GeneratedAtUtc, p.YtdGross, p.YtdTax }).ToListAsync(ct);

        var bundle = new
        {
            employee = new
            {
                employee.EmployeeCode, employee.FirstName, employee.LastName, employee.DateOfBirth,
                employee.PersonalEmail, employee.WorkEmail, employee.Phone, employee.JoinDate, employee.ExitDate, employee.Status
            },
            documents,
            leaveRequests,
            attendanceRecords,
            goals,
            feedbackReceived,
            payslips
        };

        return new EmployeeDataExportDto(JsonSerializer.Serialize(bundle, new JsonSerializerOptions { WriteIndented = true }), DateTime.UtcNow);
    }
}

public class AnonymizeEmployeeDataCommandHandler : IRequestHandler<AnonymizeEmployeeDataCommand>
{
    private readonly AppDbContext _db;
    public AnonymizeEmployeeDataCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(AnonymizeEmployeeDataCommand request, CancellationToken ct)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, ct)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        var redactedSuffix = employee.Id.ToString("N")[..8];
        employee.FirstName = "Redacted";
        employee.LastName = "Redacted";
        employee.PersonalEmail = null;
        employee.WorkEmail = $"redacted-{redactedSuffix}@redacted.invalid";
        employee.Phone = null;
        employee.DateOfBirth = null;

        await _db.SaveChangesAsync(ct);
    }
}
