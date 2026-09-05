using MediatR;
using PeopleHQ.Domain.Engagement;

namespace PeopleHQ.Application.Engagement;

// eNPS / pulse surveys (§L "Beyond Zoho"). True anonymity per the domain model: when Survey.IsAnonymous,
// SurveyResponse.RespondentEmployeeId is stored as null — no dedup tracking is possible in that mode,
// matching "anonymous by default" as modeled rather than layering in a hidden identity trail.
public record CreateSurveyCommand(string Question, bool IsAnonymous) : IRequest<Guid>;
public record DeactivateSurveyCommand(Guid Id) : IRequest;
public record GetActiveSurveysQuery : IRequest<IReadOnlyList<SurveyDto>>;
public record SurveyDto(Guid Id, string Question, bool IsAnonymous, bool IsActive);

public record SubmitSurveyResponseCommand(Guid SurveyId, int Score, string? Comment) : IRequest<Guid>;

public record GetSurveyResultsQuery(Guid SurveyId) : IRequest<SurveyResultsDto>;
public record SurveyResultsDto(Guid SurveyId, int ResponseCount, decimal AverageScore, int PromoterCount, int PassiveCount, int DetractorCount, decimal? EnpsScore);

// Asset management ("most needed options" #2) — admin/IT managed, no employee self-service actions.
public record CreateAssetCommand(string Name, string SerialNo) : IRequest<Guid>;
public record AssignAssetCommand(Guid AssetId, Guid EmployeeId) : IRequest;
public record ReturnAssetCommand(Guid AssetId) : IRequest;
public record RetireAssetCommand(Guid AssetId) : IRequest;
public record GetAssetsQuery(Guid? AssignedEmployeeId = null, AssetStatus? Status = null) : IRequest<IReadOnlyList<AssetDto>>;
public record AssetDto(Guid Id, string Name, string SerialNo, Guid? AssignedEmployeeId, AssetStatus Status);

// HR Helpdesk ("most needed options" #3). Create + list-own is self-service (HelpdeskTicketWrite, broadly
// granted); assign/status-update/list-all requires the elevated HelpdeskTicketManage permission.
public record CreateHelpdeskTicketCommand(string Category, string Subject, string? Description) : IRequest<Guid>;
public record AssignHelpdeskTicketCommand(Guid TicketId, Guid AssignedToEmployeeId, DateTime? SlaDueAtUtc) : IRequest;
public record UpdateHelpdeskTicketStatusCommand(Guid TicketId, HelpdeskTicketStatus Status) : IRequest;
public record GetHelpdeskTicketsQuery(Guid? RaisedByEmployeeId = null, HelpdeskTicketStatus? Status = null) : IRequest<IReadOnlyList<HelpdeskTicketDto>>;
public record HelpdeskTicketDto(Guid Id, Guid RaisedByEmployeeId, string Category, string Subject, string? Description, HelpdeskTicketStatus Status, Guid? AssignedToEmployeeId, DateTime? SlaDueAtUtc);

// Announcements (§H "announcements/feed"). Publishing is immediate in v1 — no draft/schedule step.
public record CreateAnnouncementCommand(string Title, string Body, string AudienceJson) : IRequest<Guid>;
public record DeleteAnnouncementCommand(Guid Id) : IRequest;
public record GetActiveAnnouncementsQuery : IRequest<IReadOnlyList<AnnouncementDto>>;
public record AnnouncementDto(Guid Id, string Title, string Body, string AudienceJson, DateTime? PublishedAtUtc);
