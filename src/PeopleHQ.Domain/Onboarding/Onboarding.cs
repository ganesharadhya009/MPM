using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.Onboarding;

public enum CandidateStage { OfferSent, Accepted, DocumentsCollected, ReadyToOnboard, Converted, Rejected }
public enum OnboardingTaskStatus { Pending, Done }

public class Candidate : TenantOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? ResumeBlobUrl { get; set; }
    public Guid? DesignationId { get; set; }
    public string? Source { get; set; }
    public CandidateStage Stage { get; set; } = CandidateStage.OfferSent;
    public Guid? ConvertedEmployeeId { get; set; }
}

public class OnboardingChecklistTemplate : TenantOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? AppliesToDepartmentId { get; set; }
    public Guid? AppliesToDesignationId { get; set; }
}

public class OnboardingChecklistItem : BaseEntity
{
    public Guid TemplateId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OwnerRole { get; set; } = string.Empty; // IT / HR / Manager
    public int DueOffsetDays { get; set; } // relative to join date
    /// <summary>"Beyond Zoho" — buddy/mentor assignment (FR-ONB-07).</summary>
    public string? BuddyRole { get; set; }
}

public class OnboardingTask : TenantOwnedEntity
{
    public Guid? CandidateId { get; set; }
    public Guid? EmployeeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? OwnerEmployeeId { get; set; }
    public DateOnly DueDate { get; set; }
    public OnboardingTaskStatus Status { get; set; } = OnboardingTaskStatus.Pending;
    public Guid? SourceItemId { get; set; }
}
