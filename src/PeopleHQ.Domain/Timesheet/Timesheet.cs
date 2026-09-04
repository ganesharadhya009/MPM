using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.Timesheet;

public enum TimesheetEntryMode { Simple, Detailed }
public enum TimesheetStatus { Draft, Submitted, Approved, Rejected }

public class Project : TenantOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // unique per tenant
    public string? ClientName { get; set; }
    public bool BillableDefault { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class ProjectTask : BaseEntity
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool? IsBillable { get; set; } // null = inherit Project.BillableDefault
}

public class Timesheet : TenantOwnedEntity
{
    public Guid EmployeeId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public TimesheetEntryMode EntryMode { get; set; } = TimesheetEntryMode.Simple;
    public TimesheetStatus Status { get; set; } = TimesheetStatus.Draft;
    public Guid? WorkflowRequestId { get; set; }
}

public class TimesheetEntry : BaseEntity
{
    public Guid TimesheetId { get; set; }
    public DateOnly WorkDate { get; set; }
    public Guid? ProjectId { get; set; } // nullable if Simple mode
    public Guid? TaskId { get; set; }
    public decimal Hours { get; set; }
    public bool IsOvertime { get; set; }
    public bool IsBillable { get; set; }
    public string? Description { get; set; }
}
