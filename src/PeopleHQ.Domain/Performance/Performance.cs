using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.Performance;

public enum GoalStatus { NotStarted, InProgress, Completed, Cancelled }
public enum KeyResultMetricType { Percent, Number, Boolean }
public enum FeedbackVisibility { Public, ManagerOnly }

public class Goal : TenantOwnedEntity
{
    public Guid EmployeeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly? TargetDate { get; set; }
    public int ProgressPercent { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.NotStarted;
}

public class OkrCycle : TenantOwnedEntity
{
    public string Name { get; set; } = string.Empty; // e.g. "Q1 2027"
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}

public class Objective : TenantOwnedEntity
{
    public Guid CycleId { get; set; }
    public Guid? OwnerEmployeeId { get; set; } // null if team/company-level
    public Guid? OwnerDepartmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? ParentObjectiveId { get; set; } // for top-down alignment
}

public class KeyResult : BaseEntity
{
    public Guid ObjectiveId { get; set; }
    public string Title { get; set; } = string.Empty;
    public KeyResultMetricType MetricType { get; set; }
    public decimal StartValue { get; set; }
    public decimal TargetValue { get; set; }
    public decimal CurrentValue { get; set; }
}

/// <summary>"Beyond Zoho" — lightweight continuous feedback, separate from formal appraisal (01-modules-functional-spec.md §I).</summary>
public class FeedbackNote : TenantOwnedEntity
{
    public Guid FromEmployeeId { get; set; }
    public Guid ToEmployeeId { get; set; }
    public string Message { get; set; } = string.Empty;
    public FeedbackVisibility Visibility { get; set; } = FeedbackVisibility.Public;
}
