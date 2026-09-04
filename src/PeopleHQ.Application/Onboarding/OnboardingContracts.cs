using MediatR;
using PeopleHQ.Application.Common;
using PeopleHQ.Domain.Onboarding;

namespace PeopleHQ.Application.Onboarding;

// --- Candidates ---
public record CreateCandidateCommand(string Name, string Email, string? Phone, string? ResumeBlobUrl, Guid? DesignationId, string? Source) : IRequest<Guid>;
public record UpdateCandidateCommand(Guid Id, string Name, string? Phone, string? ResumeBlobUrl, Guid? DesignationId, string? Source) : IRequest;
public record UpdateCandidateStageCommand(Guid Id, CandidateStage Stage) : IRequest;
public record GetCandidatesQuery(int Page = 1, int PageSize = 25, CandidateStage? Stage = null) : IRequest<PagedResult<CandidateDto>>;
public record GetCandidateByIdQuery(Guid Id) : IRequest<CandidateDto>;

public record CandidateDto(Guid Id, string Name, string Email, string? Phone, string? ResumeBlobUrl,
    Guid? DesignationId, string? Source, CandidateStage Stage, Guid? ConvertedEmployeeId);

/// <summary>FR-ONB: converts an accepted/ready candidate into an Employee and auto-generates onboarding tasks
/// from any checklist template matching the new employee's department/designation.</summary>
public record ConvertCandidateToEmployeeCommand(
    Guid CandidateId, string? WorkEmail, Guid? DepartmentId, Guid? LocationId, Guid? ManagerId, DateOnly JoinDate) : IRequest<Guid>;

// --- Checklist Templates ---
public record ChecklistItemInput(string Title, string OwnerRole, int DueOffsetDays, string? BuddyRole);
public record CreateOnboardingChecklistTemplateCommand(string Name, Guid? AppliesToDepartmentId, Guid? AppliesToDesignationId, IReadOnlyList<ChecklistItemInput> Items) : IRequest<Guid>;
public record UpdateOnboardingChecklistTemplateCommand(Guid Id, string Name, Guid? AppliesToDepartmentId, Guid? AppliesToDesignationId, IReadOnlyList<ChecklistItemInput> Items) : IRequest;
public record DeleteOnboardingChecklistTemplateCommand(Guid Id) : IRequest;
public record GetOnboardingChecklistTemplatesQuery(int Page = 1, int PageSize = 25) : IRequest<PagedResult<OnboardingChecklistTemplateDto>>;

public record OnboardingChecklistItemDto(Guid Id, string Title, string OwnerRole, int DueOffsetDays, string? BuddyRole);
public record OnboardingChecklistTemplateDto(Guid Id, string Name, Guid? AppliesToDepartmentId, Guid? AppliesToDesignationId, IReadOnlyList<OnboardingChecklistItemDto> Items);

// --- Onboarding Tasks ---
public record CreateOnboardingTaskCommand(Guid? CandidateId, Guid? EmployeeId, string Title, Guid? OwnerEmployeeId, DateOnly DueDate) : IRequest<Guid>;
public record CompleteOnboardingTaskCommand(Guid Id) : IRequest;
public record GetOnboardingTasksQuery(Guid? CandidateId = null, Guid? EmployeeId = null, OnboardingTaskStatus? Status = null) : IRequest<IReadOnlyList<OnboardingTaskDto>>;

public record OnboardingTaskDto(Guid Id, Guid? CandidateId, Guid? EmployeeId, string Title, Guid? OwnerEmployeeId, DateOnly DueDate, OnboardingTaskStatus Status);
