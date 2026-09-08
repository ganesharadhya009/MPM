using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.Appraisal;

/// <summary>
/// Appraisal cycles (01-modules-functional-spec.md §I, Phase 2+): "self-review → manager review → optional
/// calibration → final rating; configurable review templates (competency questions, rating scale)."
/// Templates use a flexible JSON question list (QuestionsJson: [{"question":"...","ratingScaleMax":5}, ...])
/// rather than a fully normalized question table — same "flexible schema via JSON" convention already used
/// elsewhere in this codebase (Plan.FeaturesJson, WorkflowChainRule.RuleJson, LeavePolicy.AppliesToJson).
/// </summary>
public enum AppraisalCycleStatus { Draft, Active, Closed }
public enum AppraisalStage { SelfReview, ManagerReview, Calibration, Completed }

public class AppraisalReviewTemplate : TenantOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string QuestionsJson { get; set; } = "[]";
}

public class AppraisalCycle : TenantOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid TemplateId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public AppraisalCycleStatus Status { get; set; } = AppraisalCycleStatus.Draft;
}

/// <summary>One row per employee per cycle, created when the cycle is activated. ManagerId is a snapshot
/// taken at activation time (matches WorkflowApprovalStep's "resolve chain once, don't re-resolve live"
/// principle — a later manager change shouldn't retroactively alter a review already in flight).</summary>
public class AppraisalReview : TenantOwnedEntity
{
    public Guid CycleId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid? ManagerId { get; set; }
    public AppraisalStage Stage { get; set; } = AppraisalStage.SelfReview;
    public string? SelfAnswersJson { get; set; }
    public string? ManagerAnswersJson { get; set; }
    public string? CalibrationNotes { get; set; }
    public decimal? FinalRating { get; set; }
    public string? FinalComments { get; set; }
    public DateTime? SelfSubmittedAtUtc { get; set; }
    public DateTime? ManagerSubmittedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
