using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.Workflow;

public enum WorkflowRequestType
{
    LeaveRequest, Regularization, TimesheetApproval, PayrollRunApproval,
    // Phase 2:
    DepartmentChange, LocationChange, DesignationChange, TravelRequest, TravelExpense, ExitRequest
}
public enum WorkflowStatus { Draft, Pending, Approved, Rejected, Cancelled, Withdrawn }
public enum ApprovalStepMode { Sequential, AnyOf, AllOf }
public enum ApprovalStepStatus { Pending, Approved, Rejected, Skipped }

/// <summary>
/// The generic Workflow Engine — one polymorphic request type backs Leave,
/// Regularization, Timesheet, Payroll Run (Phase 1) and HR-process/Travel/Exit
/// (Phase 2). Built once (01-modules-functional-spec.md §J). Payload is JSONB;
/// strongly-typed detail lives in the type's own table (LeaveRequest etc.) where
/// that type has an independent lifecycle — see 02-data-model-erd.md "Key modeling decisions" #1.
/// </summary>
public class WorkflowRequest : TenantOwnedEntity
{
    public WorkflowRequestType RequestType { get; set; }
    public Guid RequesterEmployeeId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public int CurrentStepOrder { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}

public class WorkflowApprovalStep : BaseEntity
{
    public Guid WorkflowRequestId { get; set; }
    public int StepOrder { get; set; }
    public Guid ApproverEmployeeId { get; set; }
    public ApprovalStepMode Mode { get; set; } = ApprovalStepMode.Sequential;
    public ApprovalStepStatus Status { get; set; } = ApprovalStepStatus.Pending;
    public DateTime? ActedAtUtc { get; set; }
    public string? Comment { get; set; }
    /// <summary>Delegation support — "Approved by X on behalf of Y" (FR-WF-06).</summary>
    public Guid? ActedOnBehalfOfEmployeeId { get; set; }
}

/// <summary>Drives approval-chain resolution at submission time (FR-WF-02) and the no-code chain-builder UI (Phase 4).</summary>
public class WorkflowChainRule : TenantOwnedEntity
{
    public WorkflowRequestType RequestType { get; set; }
    /// <summary>e.g. {"approver":"direct_manager"} or {"approver":"department_head","if":"days>5"}.</summary>
    public string RuleJson { get; set; } = "{}";
    public int Order { get; set; }
}

/// <summary>Approval-authority delegation for a date range (FR-WF-06).</summary>
public class Delegation : TenantOwnedEntity
{
    public Guid FromEmployeeId { get; set; }
    public Guid ToEmployeeId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}
