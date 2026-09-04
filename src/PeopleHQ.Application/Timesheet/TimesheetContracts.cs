using MediatR;
using PeopleHQ.Application.Common;
using PeopleHQ.Domain.Timesheet;

namespace PeopleHQ.Application.Timesheet;

// --- Projects ---
public record CreateProjectCommand(string Name, string Code, string? ClientName, bool BillableDefault) : IRequest<Guid>;
public record UpdateProjectCommand(Guid Id, string Name, string? ClientName, bool BillableDefault, bool IsActive) : IRequest;
public record DeleteProjectCommand(Guid Id) : IRequest;
public record GetProjectsQuery(bool? IsActive = null) : IRequest<IReadOnlyList<ProjectDto>>;
public record ProjectDto(Guid Id, string Name, string Code, string? ClientName, bool BillableDefault, bool IsActive);

public record CreateProjectTaskCommand(Guid ProjectId, string Name, bool? IsBillable) : IRequest<Guid>;
public record UpdateProjectTaskCommand(Guid Id, string Name, bool? IsBillable) : IRequest;
public record DeleteProjectTaskCommand(Guid Id) : IRequest;
public record GetProjectTasksQuery(Guid ProjectId) : IRequest<IReadOnlyList<ProjectTaskDto>>;
public record ProjectTaskDto(Guid Id, Guid ProjectId, string Name, bool? IsBillable);

// --- Timesheets (FR-TSH), routed through the generic Workflow engine ---
public record CreateTimesheetCommand(DateOnly PeriodStart, DateOnly PeriodEnd, TimesheetEntryMode EntryMode) : IRequest<Guid>;
public record AddTimesheetEntryCommand(Guid TimesheetId, DateOnly WorkDate, Guid? ProjectId, Guid? TaskId, decimal Hours, bool IsOvertime, bool IsBillable, string? Description) : IRequest<Guid>;
public record UpdateTimesheetEntryCommand(Guid Id, DateOnly WorkDate, Guid? ProjectId, Guid? TaskId, decimal Hours, bool IsOvertime, bool IsBillable, string? Description) : IRequest;
public record DeleteTimesheetEntryCommand(Guid Id) : IRequest;
public record SubmitTimesheetCommand(Guid TimesheetId) : IRequest;

public record GetTimesheetsQuery(Guid? EmployeeId = null, TimesheetStatus? Status = null) : IRequest<IReadOnlyList<TimesheetSummaryDto>>;
public record TimesheetSummaryDto(Guid Id, Guid EmployeeId, DateOnly PeriodStart, DateOnly PeriodEnd, TimesheetEntryMode EntryMode, TimesheetStatus Status, decimal TotalHours);

public record GetTimesheetByIdQuery(Guid Id) : IRequest<TimesheetDetailDto>;
public record TimesheetEntryDto(Guid Id, DateOnly WorkDate, Guid? ProjectId, Guid? TaskId, decimal Hours, bool IsOvertime, bool IsBillable, string? Description);
public record TimesheetDetailDto(Guid Id, Guid EmployeeId, DateOnly PeriodStart, DateOnly PeriodEnd, TimesheetEntryMode EntryMode,
    TimesheetStatus Status, Guid? WorkflowRequestId, IReadOnlyList<TimesheetEntryDto> Entries);
