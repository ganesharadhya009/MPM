using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Attendance;
using PeopleHQ.Application.Common;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Domain.Attendance;
using PeopleHQ.Domain.Auditing;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Attendance;

// ===== Shifts =====
public class CreateShiftCommandHandler : IRequestHandler<CreateShiftCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant; private readonly IAuditLogWriter _audit;
    public CreateShiftCommandHandler(AppDbContext db, ITenantContext tenant, IAuditLogWriter audit) { _db = db; _tenant = tenant; _audit = audit; }

    public async Task<Guid> Handle(CreateShiftCommand request, CancellationToken ct)
    {
        var shift = new Shift { TenantId = _tenant.TenantId, Name = request.Name, StartTime = request.StartTime, EndTime = request.EndTime, GraceMinutes = request.GraceMinutes, BreakMinutes = request.BreakMinutes };
        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Shift), shift.Id, AuditAction.Create, null, shift, ct);
        return shift.Id;
    }
}

public class UpdateShiftCommandHandler : IRequestHandler<UpdateShiftCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public UpdateShiftCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(UpdateShiftCommand request, CancellationToken ct)
    {
        var shift = await _db.Shifts.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Shift), request.Id);
        var before = new { shift.Name, shift.StartTime, shift.EndTime, shift.GraceMinutes, shift.BreakMinutes };
        shift.Name = request.Name; shift.StartTime = request.StartTime; shift.EndTime = request.EndTime;
        shift.GraceMinutes = request.GraceMinutes; shift.BreakMinutes = request.BreakMinutes;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Shift), shift.Id, AuditAction.Update, before, shift, ct);
    }
}

public class DeleteShiftCommandHandler : IRequestHandler<DeleteShiftCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public DeleteShiftCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(DeleteShiftCommand request, CancellationToken ct)
    {
        var shift = await _db.Shifts.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Shift), request.Id);
        var hasAssignments = await _db.ShiftAssignments.AnyAsync(a => a.ShiftId == request.Id, ct);
        if (hasAssignments) throw new ConflictException($"Shift '{shift.Name}' has employee assignments and cannot be deleted.");

        shift.IsDeleted = true; shift.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Shift), shift.Id, AuditAction.Delete, shift, null, ct);
    }
}

public class GetShiftsQueryHandler : IRequestHandler<GetShiftsQuery, PagedResult<ShiftDto>>
{
    private readonly AppDbContext _db;
    public GetShiftsQueryHandler(AppDbContext db) => _db = db;

    public async Task<PagedResult<ShiftDto>> Handle(GetShiftsQuery request, CancellationToken ct)
    {
        var query = _db.Shifts.OrderBy(s => s.Name);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(s => new ShiftDto(s.Id, s.Name, s.StartTime, s.EndTime, s.GraceMinutes, s.BreakMinutes)).ToListAsync(ct);
        return PagedResult<ShiftDto>.Create(items, request.Page, request.PageSize, total);
    }
}

public class AssignShiftCommandHandler : IRequestHandler<AssignShiftCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant;
    public AssignShiftCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<Guid> Handle(AssignShiftCommand request, CancellationToken ct)
    {
        var assignment = new ShiftAssignment { TenantId = _tenant.TenantId, EmployeeId = request.EmployeeId, ShiftId = request.ShiftId, EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo };
        _db.ShiftAssignments.Add(assignment);
        await _db.SaveChangesAsync(ct);
        return assignment.Id;
    }
}

public class GetShiftAssignmentsQueryHandler : IRequestHandler<GetShiftAssignmentsQuery, IReadOnlyList<ShiftAssignmentDto>>
{
    private readonly AppDbContext _db;
    public GetShiftAssignmentsQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ShiftAssignmentDto>> Handle(GetShiftAssignmentsQuery request, CancellationToken ct)
        => await _db.ShiftAssignments.Where(a => a.EmployeeId == request.EmployeeId).OrderByDescending(a => a.EffectiveFrom)
            .Select(a => new ShiftAssignmentDto(a.Id, a.EmployeeId, a.ShiftId, a.EffectiveFrom, a.EffectiveTo)).ToListAsync(ct);
}

// ===== Check-in / Check-out =====
public class CheckInCommandHandler : IRequestHandler<CheckInCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant; private readonly ICurrentEmployeeResolver _employeeResolver;
    public CheckInCommandHandler(AppDbContext db, ITenantContext tenant, ICurrentEmployeeResolver employeeResolver) { _db = db; _tenant = tenant; _employeeResolver = employeeResolver; }

    public async Task<Guid> Handle(CheckInCommand request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var existing = await _db.AttendanceRecords.FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == today, ct);
        if (existing is not null)
        {
            if (existing.CheckInAtUtc is not null) throw new ConflictException("Already checked in today.");
            existing.CheckInAtUtc = DateTime.UtcNow;
            existing.CheckInLat = request.Lat; existing.CheckInLng = request.Lng;
            await _db.SaveChangesAsync(ct);
            return existing.Id;
        }

        var record = new AttendanceRecord
        {
            TenantId = _tenant.TenantId, EmployeeId = employeeId, Date = today,
            CheckInAtUtc = DateTime.UtcNow, CheckInLat = request.Lat, CheckInLng = request.Lng,
            Source = AttendanceSource.Web, Status = AttendanceStatus.Present
        };
        _db.AttendanceRecords.Add(record);
        await _db.SaveChangesAsync(ct);
        return record.Id;
    }
}

public class CheckOutCommandHandler : IRequestHandler<CheckOutCommand>
{
    private readonly AppDbContext _db; private readonly ICurrentEmployeeResolver _employeeResolver;
    public CheckOutCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task Handle(CheckOutCommand request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var record = await _db.AttendanceRecords.FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == today, ct)
            ?? throw new ConflictException("No check-in found for today.");
        if (record.CheckOutAtUtc is not null) throw new ConflictException("Already checked out today.");

        record.CheckOutAtUtc = DateTime.UtcNow;
        record.OvertimeHours = await ComputeOvertimeHoursAsync(record, ct);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>FR-ATT-08: hours worked beyond the assigned shift's scheduled length (minus break, minus grace).</summary>
    private async Task<decimal> ComputeOvertimeHoursAsync(AttendanceRecord record, CancellationToken ct)
    {
        if (record.CheckInAtUtc is null || record.CheckOutAtUtc is null) return 0m;
        var workedMinutes = (record.CheckOutAtUtc.Value - record.CheckInAtUtc.Value).TotalMinutes;

        var shift = await (
            from sa in _db.ShiftAssignments
            join s in _db.Shifts on sa.ShiftId equals s.Id
            where sa.EmployeeId == record.EmployeeId && sa.EffectiveFrom <= record.Date && (sa.EffectiveTo == null || sa.EffectiveTo >= record.Date)
            orderby sa.EffectiveFrom descending
            select s
        ).FirstOrDefaultAsync(ct);
        if (shift is null) return 0m;

        var scheduledMinutes = (shift.EndTime.ToTimeSpan() - shift.StartTime.ToTimeSpan()).TotalMinutes - shift.BreakMinutes;
        var overtimeMinutes = workedMinutes - scheduledMinutes - shift.GraceMinutes;
        return overtimeMinutes > 0 ? Math.Round((decimal)(overtimeMinutes / 60.0), 2) : 0m;
    }
}

public class GetAttendanceQueryHandler : IRequestHandler<GetAttendanceQuery, PagedResult<AttendanceRecordDto>>
{
    private readonly AppDbContext _db;
    public GetAttendanceQueryHandler(AppDbContext db) => _db = db;

    public async Task<PagedResult<AttendanceRecordDto>> Handle(GetAttendanceQuery request, CancellationToken ct)
    {
        var query = _db.AttendanceRecords.AsQueryable();
        if (request.EmployeeId is not null) query = query.Where(a => a.EmployeeId == request.EmployeeId);
        if (request.DateFrom is not null) query = query.Where(a => a.Date >= request.DateFrom);
        if (request.DateTo is not null) query = query.Where(a => a.Date <= request.DateTo);
        query = query.OrderByDescending(a => a.Date);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(a => new AttendanceRecordDto(a.Id, a.EmployeeId, a.Date, a.CheckInAtUtc, a.CheckOutAtUtc, a.Source, a.Status, a.OvertimeHours))
            .ToListAsync(ct);
        return PagedResult<AttendanceRecordDto>.Create(items, request.Page, request.PageSize, total);
    }
}

// ===== Regularization =====
public class CreateRegularizationRequestCommandHandler : IRequestHandler<CreateRegularizationRequestCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly Application.Workflow.IWorkflowEngine _workflowEngine;

    public CreateRegularizationRequestCommandHandler(AppDbContext db, ITenantContext tenant, ICurrentEmployeeResolver employeeResolver, Application.Workflow.IWorkflowEngine workflowEngine)
    { _db = db; _tenant = tenant; _employeeResolver = employeeResolver; _workflowEngine = workflowEngine; }

    public async Task<Guid> Handle(CreateRegularizationRequestCommand request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var attendanceRecord = await _db.AttendanceRecords.FindAsync(new object[] { request.AttendanceRecordId }, ct)
            ?? throw new NotFoundException(nameof(AttendanceRecord), request.AttendanceRecordId);
        if (attendanceRecord.EmployeeId != employeeId) throw new ForbiddenException("You can only regularize your own attendance.");

        var regularization = new AttendanceRegularizationRequest
        {
            TenantId = _tenant.TenantId, AttendanceRecordId = request.AttendanceRecordId, EmployeeId = employeeId,
            RequestedCheckInAtUtc = request.RequestedCheckInAtUtc, RequestedCheckOutAtUtc = request.RequestedCheckOutAtUtc,
            Reason = request.Reason, Status = RegularizationStatus.Pending
        };
        _db.AttendanceRegularizationRequests.Add(regularization);
        await _db.SaveChangesAsync(ct); // need regularization.Id for the workflow payload

        var workflowRequestId = await _workflowEngine.SubmitAsync(
            Domain.Workflow.WorkflowRequestType.Regularization, employeeId,
            new { regularization.Id, regularization.AttendanceRecordId, regularization.Reason }, ct);

        regularization.WorkflowRequestId = workflowRequestId;
        await _db.SaveChangesAsync(ct);
        return regularization.Id;
    }
}

public class GetRegularizationRequestsQueryHandler : IRequestHandler<GetRegularizationRequestsQuery, IReadOnlyList<RegularizationRequestDto>>
{
    private readonly AppDbContext _db;
    public GetRegularizationRequestsQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<RegularizationRequestDto>> Handle(GetRegularizationRequestsQuery request, CancellationToken ct)
    {
        var query = _db.AttendanceRegularizationRequests.AsQueryable();
        if (request.EmployeeId is not null) query = query.Where(r => r.EmployeeId == request.EmployeeId);
        if (request.Status is not null) query = query.Where(r => r.Status == request.Status);

        return await query.OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new RegularizationRequestDto(r.Id, r.AttendanceRecordId, r.EmployeeId, r.RequestedCheckInAtUtc, r.RequestedCheckOutAtUtc, r.Reason, r.Status, r.WorkflowRequestId))
            .ToListAsync(ct);
    }
}

/// <summary>Applies the regularization to the underlying AttendanceRecord once its WorkflowRequest resolves —
/// the module-owned side effect the generic engine deliberately doesn't know about.</summary>
public class RegularizationResolvedHandler : INotificationHandler<Application.Workflow.WorkflowRequestResolvedNotification>
{
    private readonly AppDbContext _db;
    public RegularizationResolvedHandler(AppDbContext db) => _db = db;

    public async Task Handle(Application.Workflow.WorkflowRequestResolvedNotification notification, CancellationToken ct)
    {
        if (notification.RequestType != Domain.Workflow.WorkflowRequestType.Regularization) return;

        var regularization = await _db.AttendanceRegularizationRequests.FirstOrDefaultAsync(r => r.WorkflowRequestId == notification.WorkflowRequestId, ct);
        if (regularization is null) return; // not (yet) linked, or belongs to a different module's request

        if (notification.Status == Domain.Workflow.WorkflowStatus.Approved)
        {
            var attendanceRecord = await _db.AttendanceRecords.FindAsync(new object[] { regularization.AttendanceRecordId }, ct);
            if (attendanceRecord is not null)
            {
                if (regularization.RequestedCheckInAtUtc is not null) attendanceRecord.CheckInAtUtc = regularization.RequestedCheckInAtUtc;
                if (regularization.RequestedCheckOutAtUtc is not null) attendanceRecord.CheckOutAtUtc = regularization.RequestedCheckOutAtUtc;
                attendanceRecord.Status = AttendanceStatus.Present;
            }
            regularization.Status = RegularizationStatus.Approved;
        }
        else if (notification.Status == Domain.Workflow.WorkflowStatus.Rejected)
        {
            regularization.Status = RegularizationStatus.Rejected;
        }

        await _db.SaveChangesAsync(ct);
    }
}
