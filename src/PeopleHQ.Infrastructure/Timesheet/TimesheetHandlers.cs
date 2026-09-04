using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.Timesheet;
using PeopleHQ.Domain.Auditing;
using PeopleHQ.Domain.Timesheet;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Timesheets;

// ===== Projects =====
public class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant; private readonly IAuditLogWriter _audit;
    public CreateProjectCommandHandler(AppDbContext db, ITenantContext tenant, IAuditLogWriter audit) { _db = db; _tenant = tenant; _audit = audit; }

    public async Task<Guid> Handle(CreateProjectCommand request, CancellationToken ct)
    {
        var codeInUse = await _db.Projects.AnyAsync(p => p.Code == request.Code, ct);
        if (codeInUse) throw new ConflictException($"Project code '{request.Code}' is already in use.");

        var project = new Project { TenantId = _tenant.TenantId, Name = request.Name, Code = request.Code, ClientName = request.ClientName, BillableDefault = request.BillableDefault };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Project), project.Id, AuditAction.Create, null, project, ct);
        return project.Id;
    }
}

public class UpdateProjectCommandHandler : IRequestHandler<UpdateProjectCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public UpdateProjectCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(UpdateProjectCommand request, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Project), request.Id);
        var before = new { project.Name, project.ClientName, project.BillableDefault, project.IsActive };
        project.Name = request.Name; project.ClientName = request.ClientName; project.BillableDefault = request.BillableDefault; project.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Project), project.Id, AuditAction.Update, before, project, ct);
    }
}

public class DeleteProjectCommandHandler : IRequestHandler<DeleteProjectCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public DeleteProjectCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(DeleteProjectCommand request, CancellationToken ct)
    {
        var project = await _db.Projects.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Project), request.Id);
        var hasEntries = await _db.TimesheetEntries.AnyAsync(e => e.ProjectId == request.Id, ct);
        if (hasEntries) throw new ConflictException($"Project '{project.Name}' has timesheet entries and cannot be deleted.");

        project.IsDeleted = true; project.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(Project), project.Id, AuditAction.Delete, project, null, ct);
    }
}

public class GetProjectsQueryHandler : IRequestHandler<GetProjectsQuery, IReadOnlyList<ProjectDto>>
{
    private readonly AppDbContext _db;
    public GetProjectsQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ProjectDto>> Handle(GetProjectsQuery request, CancellationToken ct)
    {
        var query = _db.Projects.AsQueryable();
        if (request.IsActive is not null) query = query.Where(p => p.IsActive == request.IsActive);
        return await query.OrderBy(p => p.Name)
            .Select(p => new ProjectDto(p.Id, p.Name, p.Code, p.ClientName, p.BillableDefault, p.IsActive)).ToListAsync(ct);
    }
}

// ===== Project Tasks =====
public class CreateProjectTaskCommandHandler : IRequestHandler<CreateProjectTaskCommand, Guid>
{
    private readonly AppDbContext _db;
    public CreateProjectTaskCommandHandler(AppDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateProjectTaskCommand request, CancellationToken ct)
    {
        var projectExists = await _db.Projects.AnyAsync(p => p.Id == request.ProjectId, ct);
        if (!projectExists) throw new NotFoundException(nameof(Project), request.ProjectId);

        var task = new ProjectTask { ProjectId = request.ProjectId, Name = request.Name, IsBillable = request.IsBillable };
        _db.ProjectTasks.Add(task);
        await _db.SaveChangesAsync(ct);
        return task.Id;
    }
}

public class UpdateProjectTaskCommandHandler : IRequestHandler<UpdateProjectTaskCommand>
{
    private readonly AppDbContext _db;
    public UpdateProjectTaskCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(UpdateProjectTaskCommand request, CancellationToken ct)
    {
        var task = await _db.ProjectTasks.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(ProjectTask), request.Id);
        task.Name = request.Name; task.IsBillable = request.IsBillable;
        await _db.SaveChangesAsync(ct);
    }
}

public class DeleteProjectTaskCommandHandler : IRequestHandler<DeleteProjectTaskCommand>
{
    private readonly AppDbContext _db;
    public DeleteProjectTaskCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(DeleteProjectTaskCommand request, CancellationToken ct)
    {
        var task = await _db.ProjectTasks.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(ProjectTask), request.Id);
        var hasEntries = await _db.TimesheetEntries.AnyAsync(e => e.TaskId == request.Id, ct);
        if (hasEntries) throw new ConflictException($"Task '{task.Name}' has timesheet entries and cannot be deleted.");

        _db.ProjectTasks.Remove(task); // no soft-delete flag on this value-object-like entity (BaseEntity, not TenantOwnedEntity)
        await _db.SaveChangesAsync(ct);
    }
}

public class GetProjectTasksQueryHandler : IRequestHandler<GetProjectTasksQuery, IReadOnlyList<ProjectTaskDto>>
{
    private readonly AppDbContext _db;
    public GetProjectTasksQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ProjectTaskDto>> Handle(GetProjectTasksQuery request, CancellationToken ct)
        => await _db.ProjectTasks.Where(t => t.ProjectId == request.ProjectId).OrderBy(t => t.Name)
            .Select(t => new ProjectTaskDto(t.Id, t.ProjectId, t.Name, t.IsBillable)).ToListAsync(ct);
}

// ===== Timesheets =====
public class CreateTimesheetCommandHandler : IRequestHandler<CreateTimesheetCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant; private readonly ICurrentEmployeeResolver _employeeResolver;
    public CreateTimesheetCommandHandler(AppDbContext db, ITenantContext tenant, ICurrentEmployeeResolver employeeResolver) { _db = db; _tenant = tenant; _employeeResolver = employeeResolver; }

    public async Task<Guid> Handle(CreateTimesheetCommand request, CancellationToken ct)
    {
        if (request.PeriodEnd < request.PeriodStart) throw new ValidationException(nameof(request.PeriodEnd), "Period end must be on or after the period start.");
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);

        var overlapping = await _db.Timesheets.AnyAsync(t => t.EmployeeId == employeeId && t.PeriodStart == request.PeriodStart && t.PeriodEnd == request.PeriodEnd, ct);
        if (overlapping) throw new ConflictException("A timesheet for this period already exists.");

        var timesheet = new Domain.Timesheet.Timesheet
        {
            TenantId = _tenant.TenantId, EmployeeId = employeeId, PeriodStart = request.PeriodStart, PeriodEnd = request.PeriodEnd,
            EntryMode = request.EntryMode, Status = TimesheetStatus.Draft
        };
        _db.Timesheets.Add(timesheet);
        await _db.SaveChangesAsync(ct);
        return timesheet.Id;
    }
}

public class AddTimesheetEntryCommandHandler : IRequestHandler<AddTimesheetEntryCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public AddTimesheetEntryCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task<Guid> Handle(AddTimesheetEntryCommand request, CancellationToken ct)
    {
        var timesheet = await _db.Timesheets.FindAsync(new object[] { request.TimesheetId }, ct) ?? throw new NotFoundException(nameof(Domain.Timesheet.Timesheet), request.TimesheetId);
        // timesheet.write is granted broadly (every Employee holds it for their own timesheet) — without this
        // check any employee could add entries to another employee's timesheet by guessing/enumerating its id.
        var callerEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        if (timesheet.EmployeeId != callerEmployeeId) throw new ForbiddenException("You can only add entries to your own timesheet.");
        if (timesheet.Status != TimesheetStatus.Draft) throw new ConflictException("Entries can only be added while the timesheet is in Draft.");
        if (request.WorkDate < timesheet.PeriodStart || request.WorkDate > timesheet.PeriodEnd)
            throw new ValidationException(nameof(request.WorkDate), "Work date must fall within the timesheet's period.");

        var entry = new TimesheetEntry
        {
            TimesheetId = request.TimesheetId, WorkDate = request.WorkDate, ProjectId = request.ProjectId, TaskId = request.TaskId,
            Hours = request.Hours, IsOvertime = request.IsOvertime, IsBillable = request.IsBillable, Description = request.Description
        };
        _db.TimesheetEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
        return entry.Id;
    }
}

public class UpdateTimesheetEntryCommandHandler : IRequestHandler<UpdateTimesheetEntryCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public UpdateTimesheetEntryCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task Handle(UpdateTimesheetEntryCommand request, CancellationToken ct)
    {
        var entry = await _db.TimesheetEntries.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(TimesheetEntry), request.Id);
        var timesheet = await _db.Timesheets.FindAsync(new object[] { entry.TimesheetId }, ct);
        var callerEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        if (timesheet?.EmployeeId != callerEmployeeId) throw new ForbiddenException("You can only edit entries on your own timesheet.");
        if (timesheet?.Status != TimesheetStatus.Draft) throw new ConflictException("Entries can only be edited while the timesheet is in Draft.");

        entry.WorkDate = request.WorkDate; entry.ProjectId = request.ProjectId; entry.TaskId = request.TaskId;
        entry.Hours = request.Hours; entry.IsOvertime = request.IsOvertime; entry.IsBillable = request.IsBillable; entry.Description = request.Description;
        await _db.SaveChangesAsync(ct);
    }
}

public class DeleteTimesheetEntryCommandHandler : IRequestHandler<DeleteTimesheetEntryCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public DeleteTimesheetEntryCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task Handle(DeleteTimesheetEntryCommand request, CancellationToken ct)
    {
        var entry = await _db.TimesheetEntries.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(TimesheetEntry), request.Id);
        var timesheet = await _db.Timesheets.FindAsync(new object[] { entry.TimesheetId }, ct);
        var callerEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        if (timesheet?.EmployeeId != callerEmployeeId) throw new ForbiddenException("You can only remove entries from your own timesheet.");
        if (timesheet?.Status != TimesheetStatus.Draft) throw new ConflictException("Entries can only be removed while the timesheet is in Draft.");

        _db.TimesheetEntries.Remove(entry);
        await _db.SaveChangesAsync(ct);
    }
}

public class SubmitTimesheetCommandHandler : IRequestHandler<SubmitTimesheetCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly Application.Workflow.IWorkflowEngine _workflowEngine;

    public SubmitTimesheetCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver, Application.Workflow.IWorkflowEngine workflowEngine)
    { _db = db; _employeeResolver = employeeResolver; _workflowEngine = workflowEngine; }

    public async Task Handle(SubmitTimesheetCommand request, CancellationToken ct)
    {
        var timesheet = await _db.Timesheets.FindAsync(new object[] { request.TimesheetId }, ct) ?? throw new NotFoundException(nameof(Domain.Timesheet.Timesheet), request.TimesheetId);
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        if (timesheet.EmployeeId != employeeId) throw new ForbiddenException("You can only submit your own timesheet.");
        if (timesheet.Status != TimesheetStatus.Draft) throw new ConflictException("Only a Draft timesheet can be submitted.");

        var hasEntries = await _db.TimesheetEntries.AnyAsync(e => e.TimesheetId == timesheet.Id, ct);
        if (!hasEntries) throw new ValidationException(nameof(request.TimesheetId), "Cannot submit a timesheet with no entries.");
        var totalHours = await _db.TimesheetEntries.Where(e => e.TimesheetId == timesheet.Id).SumAsync(e => e.Hours, ct);

        timesheet.Status = TimesheetStatus.Submitted;
        var workflowRequestId = await _workflowEngine.SubmitAsync(
            Domain.Workflow.WorkflowRequestType.TimesheetApproval, employeeId,
            new { timesheet.Id, timesheet.PeriodStart, timesheet.PeriodEnd, TotalHours = totalHours }, ct);

        timesheet.WorkflowRequestId = workflowRequestId;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetTimesheetsQueryHandler : IRequestHandler<GetTimesheetsQuery, IReadOnlyList<TimesheetSummaryDto>>
{
    private readonly AppDbContext _db;
    public GetTimesheetsQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<TimesheetSummaryDto>> Handle(GetTimesheetsQuery request, CancellationToken ct)
    {
        var query = _db.Timesheets.AsQueryable();
        if (request.EmployeeId is not null) query = query.Where(t => t.EmployeeId == request.EmployeeId);
        if (request.Status is not null) query = query.Where(t => t.Status == request.Status);

        var timesheets = await query.OrderByDescending(t => t.PeriodStart).ToListAsync(ct);
        var timesheetIds = timesheets.Select(t => t.Id).ToList();
        var hoursByTimesheet = (await _db.TimesheetEntries.Where(e => timesheetIds.Contains(e.TimesheetId)).ToListAsync(ct))
            .GroupBy(e => e.TimesheetId).ToDictionary(g => g.Key, g => g.Sum(e => e.Hours));

        return timesheets.Select(t => new TimesheetSummaryDto(t.Id, t.EmployeeId, t.PeriodStart, t.PeriodEnd, t.EntryMode, t.Status,
            hoursByTimesheet.TryGetValue(t.Id, out var hours) ? hours : 0m)).ToList();
    }
}

public class GetTimesheetByIdQueryHandler : IRequestHandler<GetTimesheetByIdQuery, TimesheetDetailDto>
{
    private readonly AppDbContext _db;
    public GetTimesheetByIdQueryHandler(AppDbContext db) => _db = db;

    public async Task<TimesheetDetailDto> Handle(GetTimesheetByIdQuery request, CancellationToken ct)
    {
        var timesheet = await _db.Timesheets.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Domain.Timesheet.Timesheet), request.Id);
        var entries = await _db.TimesheetEntries.Where(e => e.TimesheetId == timesheet.Id).OrderBy(e => e.WorkDate)
            .Select(e => new TimesheetEntryDto(e.Id, e.WorkDate, e.ProjectId, e.TaskId, e.Hours, e.IsOvertime, e.IsBillable, e.Description))
            .ToListAsync(ct);

        return new TimesheetDetailDto(timesheet.Id, timesheet.EmployeeId, timesheet.PeriodStart, timesheet.PeriodEnd,
            timesheet.EntryMode, timesheet.Status, timesheet.WorkflowRequestId, entries);
    }
}

/// <summary>Finalizes the Timesheet's status once its WorkflowRequest resolves — Approved locks it in; Rejected
/// (or Withdrawn) reopens it to Draft so the employee can correct and resubmit.</summary>
public class TimesheetResolvedHandler : INotificationHandler<Application.Workflow.WorkflowRequestResolvedNotification>
{
    private readonly AppDbContext _db;
    public TimesheetResolvedHandler(AppDbContext db) => _db = db;

    public async Task Handle(Application.Workflow.WorkflowRequestResolvedNotification notification, CancellationToken ct)
    {
        if (notification.RequestType != Domain.Workflow.WorkflowRequestType.TimesheetApproval) return;

        var timesheet = await _db.Timesheets.FirstOrDefaultAsync(t => t.WorkflowRequestId == notification.WorkflowRequestId, ct);
        if (timesheet is null) return;

        if (notification.Status == Domain.Workflow.WorkflowStatus.Approved)
        {
            timesheet.Status = TimesheetStatus.Approved;
        }
        else if (notification.Status is Domain.Workflow.WorkflowStatus.Rejected or Domain.Workflow.WorkflowStatus.Withdrawn)
        {
            timesheet.Status = TimesheetStatus.Draft; // reopened for correction and resubmission
            timesheet.WorkflowRequestId = null;
        }

        await _db.SaveChangesAsync(ct);
    }
}
