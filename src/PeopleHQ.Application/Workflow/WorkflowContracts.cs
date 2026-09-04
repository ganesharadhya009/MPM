using MediatR;
using PeopleHQ.Domain.Workflow;

namespace PeopleHQ.Application.Workflow;

/// <summary>
/// The single entry point every module (Leave, Regularization, Timesheet, Payroll Run, and Phase 2's
/// HR-process requests) uses to submit into and act on the generic approval chain (01-modules-functional-spec.md §J).
/// Module handlers call SubmitAsync when creating their own request row, then persist the returned WorkflowRequestId
/// alongside their strongly-typed detail row (e.g. LeaveRequest.WorkflowRequestId).
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>Resolves the approver chain (WorkflowChainRule, falling back to direct-manager) and creates the
    /// WorkflowRequest + its WorkflowApprovalStep rows, immediately Pending on step 1.</summary>
    Task<Guid> SubmitAsync(WorkflowRequestType requestType, Guid requesterEmployeeId, object payload, CancellationToken ct = default);

    Task ApproveCurrentStepAsync(Guid workflowRequestId, Guid actingEmployeeId, string? comment, CancellationToken ct = default);
    Task RejectCurrentStepAsync(Guid workflowRequestId, Guid actingEmployeeId, string? comment, CancellationToken ct = default);
    Task WithdrawAsync(Guid workflowRequestId, Guid requesterEmployeeId, CancellationToken ct = default);
}

// --- Unified inbox / My Requests (Phase 2 ESS/MSS, exposed from Phase 1 for API completeness) ---
public record GetMyPendingApprovalsQuery : IRequest<IReadOnlyList<PendingApprovalDto>>;
public record PendingApprovalDto(Guid WorkflowRequestId, WorkflowRequestType RequestType, Guid RequesterEmployeeId,
    string PayloadJson, int StepOrder, DateTime? SubmittedAtUtc);

public record GetMyRequestsQuery(WorkflowStatus? Status = null) : IRequest<IReadOnlyList<MyRequestDto>>;
public record MyRequestDto(Guid Id, WorkflowRequestType RequestType, string PayloadJson, WorkflowStatus Status,
    int CurrentStepOrder, DateTime? SubmittedAtUtc, DateTime? ResolvedAtUtc);

public record ApproveWorkflowRequestCommand(Guid WorkflowRequestId, string? Comment) : IRequest;
public record RejectWorkflowRequestCommand(Guid WorkflowRequestId, string? Comment) : IRequest;
public record WithdrawWorkflowRequestCommand(Guid WorkflowRequestId) : IRequest;

/// <summary>Published by IWorkflowEngine whenever a WorkflowRequest reaches a terminal status (Approved/Rejected/
/// Withdrawn). Keeps the engine generic: each module (Attendance regularization, Leave, Timesheet, Payroll Run)
/// owns its own INotificationHandler that applies the type-specific side effect instead of the engine knowing
/// about every module's domain.</summary>
public record WorkflowRequestResolvedNotification(Guid WorkflowRequestId, WorkflowRequestType RequestType, WorkflowStatus Status) : INotification;

// --- Chain rules (no-code chain builder lands in Phase 4; this is the CRUD it will sit on top of) ---
public record CreateWorkflowChainRuleCommand(WorkflowRequestType RequestType, string RuleJson, int Order) : IRequest<Guid>;
public record DeleteWorkflowChainRuleCommand(Guid Id) : IRequest;
public record GetWorkflowChainRulesQuery(WorkflowRequestType RequestType) : IRequest<IReadOnlyList<WorkflowChainRuleDto>>;
public record WorkflowChainRuleDto(Guid Id, WorkflowRequestType RequestType, string RuleJson, int Order);

// --- Delegation (FR-WF-06) ---
public record CreateDelegationCommand(Guid ToEmployeeId, DateOnly StartDate, DateOnly EndDate) : IRequest<Guid>;
public record DeleteDelegationCommand(Guid Id) : IRequest;
public record GetMyDelegationsQuery : IRequest<IReadOnlyList<DelegationDto>>;
public record DelegationDto(Guid Id, Guid FromEmployeeId, Guid ToEmployeeId, DateOnly StartDate, DateOnly EndDate);
