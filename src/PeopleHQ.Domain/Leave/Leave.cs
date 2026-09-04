using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.Leave;

public enum LeaveAccrualType { Fixed, Monthly }
public enum LeaveRequestStatus { Draft, Pending, Approved, Rejected, Cancelled, Withdrawn }

public class LeaveType : TenantOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public LeaveAccrualType AccrualType { get; set; } = LeaveAccrualType.Fixed;
    public decimal AnnualEntitlement { get; set; }
    public decimal? CarryForwardCap { get; set; }
    public int? RequiresDocumentAfterDays { get; set; }
}

public class LeavePolicy : TenantOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    /// <summary>Rule filter: department/location/employment-type — serialized JSON.</summary>
    public string AppliesToJson { get; set; } = "{}";
}

public class LeaveTypePolicyRule
{
    public Guid PolicyId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public decimal? EntitlementOverride { get; set; }
}

public class EmployeeLeavePolicy
{
    public Guid EmployeeId { get; set; }
    public Guid PolicyId { get; set; }
}

public class LeaveBalance
{
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public int Year { get; set; }
    public decimal Accrued { get; set; }
    public decimal Used { get; set; }
    public decimal CarriedForward { get; set; }
    /// <summary>Provisional hold on submission, released on reject/withdraw, finalized on approval (FR-LVE-06).</summary>
    public decimal Reserved { get; set; }
}

public class LeaveRequest : TenantOwnedEntity
{
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsHalfDay { get; set; }
    public string? Reason { get; set; }
    public string? AttachmentBlobUrl { get; set; }
    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Draft;
    public Guid? WorkflowRequestId { get; set; }
}

/// <summary>
/// "Beyond Zoho" — blackout periods warn (non-blocking by default) at request time
/// (01-modules-functional-spec.md §G, FR-LVE-12).
/// </summary>
public class LeaveBlackoutPeriod : TenantOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsBlocking { get; set; } // false = warning only, tenant-configurable
}
