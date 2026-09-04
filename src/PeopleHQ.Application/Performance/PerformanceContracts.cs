using MediatR;
using PeopleHQ.Domain.Performance;

namespace PeopleHQ.Application.Performance;

// Goals (01-modules-functional-spec.md §I). EmployeeId is caller-or-direct-report-scoped: a manager may
// create/update/delete goals for their own direct reports (matches spec "manager can add goals for
// reportees"); GoalWrite alone does not grant access to arbitrary employees' goals — see GoalHandlers.

public record CreateGoalCommand(Guid EmployeeId, string Title, string? Description, DateOnly? TargetDate) : IRequest<Guid>;
public record UpdateGoalCommand(Guid Id, string Title, string? Description, DateOnly? TargetDate, int ProgressPercent, GoalStatus Status) : IRequest;
public record DeleteGoalCommand(Guid Id) : IRequest;
public record GetGoalsQuery(Guid? EmployeeId = null) : IRequest<IReadOnlyList<GoalDto>>;
public record GoalDto(Guid Id, Guid EmployeeId, string Title, string? Description, DateOnly? TargetDate, int ProgressPercent, GoalStatus Status);

// OKR cycles: tenant-wide administration, gated by the elevated OkrCycleWrite permission (distinct from
// OkrWrite, which scopes Objective/KeyResult self-service — mirrors the CustomField definition/value split).
public record CreateOkrCycleCommand(string Name, DateOnly StartDate, DateOnly EndDate) : IRequest<Guid>;
public record UpdateOkrCycleCommand(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate) : IRequest;
public record DeleteOkrCycleCommand(Guid Id) : IRequest;
public record GetOkrCyclesQuery : IRequest<IReadOnlyList<OkrCycleDto>>;
public record OkrCycleDto(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate);

// Objectives/KeyResults: an Objective with a null OwnerEmployeeId is company/department-level and requires
// OkrCycleWrite to create; an Objective owned by the caller (or, for a manager, by a direct report) only
// requires OkrWrite — see ObjectiveHandlers for the exact check.
public record CreateObjectiveCommand(Guid CycleId, Guid? OwnerEmployeeId, Guid? OwnerDepartmentId, string Title, Guid? ParentObjectiveId) : IRequest<Guid>;
public record UpdateObjectiveCommand(Guid Id, string Title, Guid? ParentObjectiveId) : IRequest;
public record DeleteObjectiveCommand(Guid Id) : IRequest;
public record GetObjectivesQuery(Guid? CycleId = null, Guid? OwnerEmployeeId = null) : IRequest<IReadOnlyList<ObjectiveDto>>;
public record KeyResultDto(Guid Id, Guid ObjectiveId, string Title, KeyResultMetricType MetricType, decimal StartValue, decimal TargetValue, decimal CurrentValue);
public record ObjectiveDto(Guid Id, Guid CycleId, Guid? OwnerEmployeeId, Guid? OwnerDepartmentId, string Title, Guid? ParentObjectiveId, IReadOnlyList<KeyResultDto> KeyResults);

public record CreateKeyResultCommand(Guid ObjectiveId, string Title, KeyResultMetricType MetricType, decimal StartValue, decimal TargetValue) : IRequest<Guid>;
public record UpdateKeyResultProgressCommand(Guid Id, decimal CurrentValue) : IRequest;
public record DeleteKeyResultCommand(Guid Id) : IRequest;

// Continuous feedback (§I "Beyond Zoho"): ToEmployeeId is any colleague; FromEmployeeId is always the
// caller. Visibility.Public is visible tenant-wide (matches "visible on their profile timeline");
// ManagerOnly is visible only to the author, the target employee, and the target's direct manager.
public record CreateFeedbackNoteCommand(Guid ToEmployeeId, string Message, FeedbackVisibility Visibility) : IRequest<Guid>;
public record GetFeedbackForEmployeeQuery(Guid EmployeeId) : IRequest<IReadOnlyList<FeedbackNoteDto>>;
public record FeedbackNoteDto(Guid Id, Guid FromEmployeeId, Guid ToEmployeeId, string Message, FeedbackVisibility Visibility, DateTime CreatedAtUtc);
