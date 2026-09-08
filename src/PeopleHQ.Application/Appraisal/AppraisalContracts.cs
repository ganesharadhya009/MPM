using MediatR;
using PeopleHQ.Domain.Appraisal;

namespace PeopleHQ.Application.Appraisal;

// Appraisal cycles (01-modules-functional-spec.md §I, Phase 2+): self-review -> manager review -> optional
// calibration -> final rating. Self-review submission always acts on the CALLER's own review; manager
// review submission requires the caller to be that review's snapshotted ManagerId (or hold the elevated
// AppraisalReviewManage-as-admin path via ReportRead, mirroring the Insights/Performance ownership pattern).

public record CreateAppraisalTemplateCommand(string Name, string QuestionsJson) : IRequest<Guid>;
public record GetAppraisalTemplatesQuery : IRequest<IReadOnlyList<AppraisalTemplateDto>>;
public record AppraisalTemplateDto(Guid Id, string Name, string QuestionsJson);

public record CreateAppraisalCycleCommand(string Name, Guid TemplateId, DateOnly StartDate, DateOnly EndDate) : IRequest<Guid>;
public record ActivateAppraisalCycleCommand(Guid CycleId) : IRequest;
public record CloseAppraisalCycleCommand(Guid CycleId) : IRequest;
public record GetAppraisalCyclesQuery : IRequest<IReadOnlyList<AppraisalCycleDto>>;
public record AppraisalCycleDto(Guid Id, string Name, Guid TemplateId, DateOnly StartDate, DateOnly EndDate, AppraisalCycleStatus Status);

public record SubmitSelfReviewCommand(Guid ReviewId, string AnswersJson) : IRequest;
public record SubmitManagerReviewCommand(Guid ReviewId, string AnswersJson) : IRequest;
public record FinalizeAppraisalReviewCommand(Guid ReviewId, decimal FinalRating, string? FinalComments, string? CalibrationNotes) : IRequest;

public record GetMyAppraisalReviewsQuery : IRequest<IReadOnlyList<AppraisalReviewDto>>;
public record GetTeamAppraisalReviewsQuery(Guid CycleId) : IRequest<IReadOnlyList<AppraisalReviewDto>>;
public record AppraisalReviewDto(Guid Id, Guid CycleId, Guid EmployeeId, Guid? ManagerId, AppraisalStage Stage,
    string? SelfAnswersJson, string? ManagerAnswersJson, decimal? FinalRating, string? FinalComments);
