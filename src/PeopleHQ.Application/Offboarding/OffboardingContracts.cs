using MediatR;
using PeopleHQ.Domain.Offboarding;

namespace PeopleHQ.Application.Offboarding;

// Exit/offboarding clearance checklist — mirrors PeopleHQ.Application.Onboarding's shape exactly.
// OffboardingTask rows are cloned from the matching template when an ExitRequest workflow is approved
// (see HrProcessResolvedHandler), due-dated from the employee's proposed last working day.

// --- Checklist Templates ---
public record OffboardingChecklistItemInput(string Title, string OwnerRole, int DueOffsetDays);
public record CreateOffboardingChecklistTemplateCommand(string Name, Guid? AppliesToDepartmentId, Guid? AppliesToDesignationId, IReadOnlyList<OffboardingChecklistItemInput> Items) : IRequest<Guid>;
public record UpdateOffboardingChecklistTemplateCommand(Guid Id, string Name, Guid? AppliesToDepartmentId, Guid? AppliesToDesignationId, IReadOnlyList<OffboardingChecklistItemInput> Items) : IRequest;
public record DeleteOffboardingChecklistTemplateCommand(Guid Id) : IRequest;
public record GetOffboardingChecklistTemplatesQuery : IRequest<IReadOnlyList<OffboardingChecklistTemplateDto>>;

public record OffboardingChecklistItemDto(Guid Id, string Title, string OwnerRole, int DueOffsetDays);
public record OffboardingChecklistTemplateDto(Guid Id, string Name, Guid? AppliesToDepartmentId, Guid? AppliesToDesignationId, IReadOnlyList<OffboardingChecklistItemDto> Items);

// --- Offboarding Tasks ---
public record CompleteOffboardingTaskCommand(Guid Id) : IRequest;
public record GetOffboardingTasksQuery(Guid? EmployeeId = null, OffboardingTaskStatus? Status = null) : IRequest<IReadOnlyList<OffboardingTaskDto>>;
public record OffboardingTaskDto(Guid Id, Guid EmployeeId, string Title, Guid? OwnerEmployeeId, DateOnly DueDate, OffboardingTaskStatus Status);
