using MediatR;
using PeopleHQ.Application.Common;
using PeopleHQ.Domain.Leave;

namespace PeopleHQ.Application.Leave;

// --- Leave Types ---
public record CreateLeaveTypeCommand(string Name, LeaveAccrualType AccrualType, decimal AnnualEntitlement, decimal? CarryForwardCap, int? RequiresDocumentAfterDays) : IRequest<Guid>;
public record UpdateLeaveTypeCommand(Guid Id, string Name, LeaveAccrualType AccrualType, decimal AnnualEntitlement, decimal? CarryForwardCap, int? RequiresDocumentAfterDays) : IRequest;
public record DeleteLeaveTypeCommand(Guid Id) : IRequest;
public record GetLeaveTypesQuery : IRequest<IReadOnlyList<LeaveTypeDto>>;
public record LeaveTypeDto(Guid Id, string Name, LeaveAccrualType AccrualType, decimal AnnualEntitlement, decimal? CarryForwardCap, int? RequiresDocumentAfterDays);

// --- Leave Policies ---
public record PolicyRuleInput(Guid LeaveTypeId, decimal? EntitlementOverride);
public record CreateLeavePolicyCommand(string Name, string AppliesToJson, IReadOnlyList<PolicyRuleInput> Rules) : IRequest<Guid>;
public record UpdateLeavePolicyCommand(Guid Id, string Name, string AppliesToJson, IReadOnlyList<PolicyRuleInput> Rules) : IRequest;
public record DeleteLeavePolicyCommand(Guid Id) : IRequest;
public record GetLeavePoliciesQuery : IRequest<IReadOnlyList<LeavePolicyDto>>;
public record LeavePolicyRuleDto(Guid LeaveTypeId, decimal? EntitlementOverride);
public record LeavePolicyDto(Guid Id, string Name, string AppliesToJson, IReadOnlyList<LeavePolicyRuleDto> Rules);

public record AssignLeavePolicyCommand(Guid EmployeeId, Guid PolicyId) : IRequest;

// --- Balances ---
public record GetLeaveBalancesQuery(Guid EmployeeId, int Year) : IRequest<IReadOnlyList<LeaveBalanceDto>>;
public record LeaveBalanceDto(Guid LeaveTypeId, int Year, decimal Accrued, decimal Used, decimal CarriedForward, decimal Reserved, decimal Available);

// --- Leave Requests (FR-LVE), routed through the generic Workflow engine ---
public record ApplyLeaveCommand(Guid LeaveTypeId, DateOnly StartDate, DateOnly EndDate, bool IsHalfDay, string? Reason, string? AttachmentBlobUrl) : IRequest<Guid>;
public record GetLeaveRequestsQuery(Guid? EmployeeId = null, LeaveRequestStatus? Status = null) : IRequest<IReadOnlyList<LeaveRequestDto>>;
public record LeaveRequestDto(Guid Id, Guid EmployeeId, Guid LeaveTypeId, DateOnly StartDate, DateOnly EndDate, bool IsHalfDay,
    string? Reason, LeaveRequestStatus Status, Guid? WorkflowRequestId);

/// <summary>FR-LVE team calendar: approved/pending leave for a manager's direct reportees in a date range.</summary>
public record GetTeamLeaveCalendarQuery(Guid ManagerId, DateOnly From, DateOnly To) : IRequest<IReadOnlyList<TeamLeaveCalendarEntryDto>>;
public record TeamLeaveCalendarEntryDto(Guid EmployeeId, Guid LeaveRequestId, Guid LeaveTypeId, DateOnly StartDate, DateOnly EndDate, LeaveRequestStatus Status);

/// <summary>"Beyond Zoho" — Bradford Factor (S² × D) absence-pattern score over approved leave in the given year (01-modules-functional-spec.md §G).</summary>
public record GetBradfordScoreQuery(Guid EmployeeId, int Year) : IRequest<BradfordScoreDto>;
public record BradfordScoreDto(Guid EmployeeId, int Year, int Spells, decimal TotalDays, decimal Score);

// --- Blackout Periods ---
public record CreateLeaveBlackoutPeriodCommand(string Name, DateOnly StartDate, DateOnly EndDate, bool IsBlocking) : IRequest<Guid>;
public record DeleteLeaveBlackoutPeriodCommand(Guid Id) : IRequest;
public record GetLeaveBlackoutPeriodsQuery : IRequest<IReadOnlyList<LeaveBlackoutPeriodDto>>;
public record LeaveBlackoutPeriodDto(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, bool IsBlocking);

/// <summary>Shared by ApplyLeaveCommandHandler and LeaveRequestResolvedHandler so both compute the same day count
/// for a request instead of duplicating (and risking drift in) the arithmetic.</summary>
public static class LeaveDayCalculator
{
    public static decimal CalculateDays(DateOnly startDate, DateOnly endDate, bool isHalfDay)
    {
        if (isHalfDay) return 0.5m;
        return endDate.DayNumber - startDate.DayNumber + 1;
    }
}
