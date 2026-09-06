using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.Offboarding;

/// <summary>
/// Exit/offboarding clearance checklist ("most needed options" #5 / 05-enhancements-and-roadmap.md Phase 4):
/// "mirror of onboarding: clearance checklist (IT, Finance, Manager)". Distinct from Full &amp; Final
/// Settlement (Phase 1 payroll calculation, PeopleHQ.Domain.Payroll) — this tracks sign-off task status
/// only. Mirrors PeopleHQ.Domain.Onboarding's shape exactly.
/// </summary>
public enum OffboardingTaskStatus { Pending, Done }

public class OffboardingChecklistTemplate : TenantOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? AppliesToDepartmentId { get; set; }
    public Guid? AppliesToDesignationId { get; set; }
}

public class OffboardingChecklistItem : BaseEntity
{
    public Guid TemplateId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OwnerRole { get; set; } = string.Empty; // IT / Finance / Manager
    /// <summary>Relative to the employee's last working day.</summary>
    public int DueOffsetDays { get; set; }
}

public class OffboardingTask : TenantOwnedEntity
{
    public Guid EmployeeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? OwnerEmployeeId { get; set; }
    public DateOnly DueDate { get; set; }
    public OffboardingTaskStatus Status { get; set; } = OffboardingTaskStatus.Pending;
    public Guid? SourceItemId { get; set; }
}
