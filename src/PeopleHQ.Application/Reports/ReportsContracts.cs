using MediatR;
using PeopleHQ.Domain.Workflow;

namespace PeopleHQ.Application.Reports;

// Standard reports (01-modules-functional-spec.md §L). All are tenant-wide aggregates, gated by the
// existing ReportRead permission (TenantAdmin only per SystemRoleSeeder) — no per-employee IDOR surface.
//
// v1 simplifications (documented, not silent):
//  - Headcount reflects the CURRENT Employee table only. True point-in-time history would read
//    EmployeePositionHistory, but no handler in this codebase writes that table yet — a tracked follow-up,
//    not a Phase 3 blocker.
//  - Attrition's "average headcount" is the mean of the period's start/end headcount, not a true
//    day-weighted integral over the period.
//  - Onboarding "days to productivity" uses OnboardingTask.UpdatedAtUtc (set when Status flips to Done) as
//    a proxy completion timestamp, since the entity has no dedicated CompletedAtUtc field.

public record GetHeadcountReportQuery : IRequest<HeadcountReportDto>;
public record HeadcountBreakdownRow(Guid? GroupId, string GroupName, int Count);
public record HeadcountReportDto(int TotalActive, IReadOnlyList<HeadcountBreakdownRow> ByDepartment, IReadOnlyList<HeadcountBreakdownRow> ByLocation, IReadOnlyList<HeadcountBreakdownRow> ByDesignation);

public record GetAttritionReportQuery(DateOnly StartDate, DateOnly EndDate) : IRequest<AttritionReportDto>;
public record AttritionReportDto(int ExitedCount, decimal AverageHeadcount, decimal AttritionRatePercent);

public record GetLeaveUtilizationReportQuery(int Year) : IRequest<IReadOnlyList<LeaveUtilizationRowDto>>;
public record LeaveUtilizationRowDto(Guid LeaveTypeId, string LeaveTypeName, decimal TotalAccrued, decimal TotalUsed, decimal UtilizationPercent);

public record GetAttendanceSummaryReportQuery(DateOnly StartDate, DateOnly EndDate) : IRequest<IReadOnlyList<AttendanceSummaryRowDto>>;
public record AttendanceSummaryRowDto(Guid EmployeeId, string EmployeeCode, string FullName, int PresentDays, int AbsentDays, int HalfDays, int OnLeaveDays, decimal TotalOvertimeHours);

public record GetOnboardingTimeToProductivityReportQuery : IRequest<IReadOnlyList<OnboardingTimeToProductivityRowDto>>;
public record OnboardingTimeToProductivityRowDto(Guid EmployeeId, string EmployeeCode, DateOnly JoinDate, int TotalTasks, int CompletedTasks, int? DaysToComplete);

public record GetApprovalSlaReportQuery(DateOnly StartDate, DateOnly EndDate) : IRequest<IReadOnlyList<ApprovalSlaRowDto>>;
public record ApprovalSlaRowDto(WorkflowRequestType RequestType, int ResolvedCount, decimal AverageHoursToResolve);
