using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Appraisal;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Domain.Appraisal;
using PeopleHQ.Domain.Employees;
using PeopleHQ.Domain.Identity;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Appraisal;

public class CreateAppraisalTemplateCommandHandler : IRequestHandler<CreateAppraisalTemplateCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public CreateAppraisalTemplateCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<Guid> Handle(CreateAppraisalTemplateCommand request, CancellationToken ct)
    {
        var template = new AppraisalReviewTemplate { TenantId = _tenant.TenantId, Name = request.Name, QuestionsJson = request.QuestionsJson };
        _db.AppraisalReviewTemplates.Add(template);
        await _db.SaveChangesAsync(ct);
        return template.Id;
    }
}

public class GetAppraisalTemplatesQueryHandler : IRequestHandler<GetAppraisalTemplatesQuery, IReadOnlyList<AppraisalTemplateDto>>
{
    private readonly AppDbContext _db;
    public GetAppraisalTemplatesQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<AppraisalTemplateDto>> Handle(GetAppraisalTemplatesQuery request, CancellationToken ct)
        => await _db.AppraisalReviewTemplates.OrderBy(t => t.Name)
            .Select(t => new AppraisalTemplateDto(t.Id, t.Name, t.QuestionsJson)).ToListAsync(ct);
}

public class CreateAppraisalCycleCommandHandler : IRequestHandler<CreateAppraisalCycleCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public CreateAppraisalCycleCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<Guid> Handle(CreateAppraisalCycleCommand request, CancellationToken ct)
    {
        var templateExists = await _db.AppraisalReviewTemplates.AnyAsync(t => t.Id == request.TemplateId, ct);
        if (!templateExists) throw new NotFoundException(nameof(AppraisalReviewTemplate), request.TemplateId);

        var cycle = new AppraisalCycle
        {
            TenantId = _tenant.TenantId, Name = request.Name, TemplateId = request.TemplateId,
            StartDate = request.StartDate, EndDate = request.EndDate, Status = AppraisalCycleStatus.Draft
        };
        _db.AppraisalCycles.Add(cycle);
        await _db.SaveChangesAsync(ct);
        return cycle.Id;
    }
}

/// <summary>Provisions one AppraisalReview per active employee, snapshotting their current ManagerId —
/// mirrors WorkflowEngine resolving an approver chain once at submission time rather than re-resolving it
/// live, so a manager change mid-cycle doesn't retroactively alter reviews already in flight.</summary>
public class ActivateAppraisalCycleCommandHandler : IRequestHandler<ActivateAppraisalCycleCommand>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public ActivateAppraisalCycleCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task Handle(ActivateAppraisalCycleCommand request, CancellationToken ct)
    {
        var cycle = await _db.AppraisalCycles.FirstOrDefaultAsync(c => c.Id == request.CycleId, ct)
            ?? throw new NotFoundException(nameof(AppraisalCycle), request.CycleId);
        if (cycle.Status != AppraisalCycleStatus.Draft) throw new ConflictException("Only a draft cycle can be activated.");

        var activeEmployees = await _db.Employees.Where(e => e.Status == EmployeeStatus.Active)
            .Select(e => new { e.Id, e.ManagerId }).ToListAsync(ct);

        foreach (var employee in activeEmployees)
        {
            _db.AppraisalReviews.Add(new AppraisalReview
            {
                TenantId = _tenant.TenantId, CycleId = cycle.Id, EmployeeId = employee.Id, ManagerId = employee.ManagerId,
                Stage = AppraisalStage.SelfReview
            });
        }

        cycle.Status = AppraisalCycleStatus.Active;
        await _db.SaveChangesAsync(ct);
    }
}

public class CloseAppraisalCycleCommandHandler : IRequestHandler<CloseAppraisalCycleCommand>
{
    private readonly AppDbContext _db;
    public CloseAppraisalCycleCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(CloseAppraisalCycleCommand request, CancellationToken ct)
    {
        var cycle = await _db.AppraisalCycles.FirstOrDefaultAsync(c => c.Id == request.CycleId, ct)
            ?? throw new NotFoundException(nameof(AppraisalCycle), request.CycleId);
        cycle.Status = AppraisalCycleStatus.Closed;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetAppraisalCyclesQueryHandler : IRequestHandler<GetAppraisalCyclesQuery, IReadOnlyList<AppraisalCycleDto>>
{
    private readonly AppDbContext _db;
    public GetAppraisalCyclesQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<AppraisalCycleDto>> Handle(GetAppraisalCyclesQuery request, CancellationToken ct)
        => await _db.AppraisalCycles.OrderByDescending(c => c.StartDate)
            .Select(c => new AppraisalCycleDto(c.Id, c.Name, c.TemplateId, c.StartDate, c.EndDate, c.Status)).ToListAsync(ct);
}

public class SubmitSelfReviewCommandHandler : IRequestHandler<SubmitSelfReviewCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public SubmitSelfReviewCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task Handle(SubmitSelfReviewCommand request, CancellationToken ct)
    {
        var review = await _db.AppraisalReviews.FirstOrDefaultAsync(r => r.Id == request.ReviewId, ct)
            ?? throw new NotFoundException(nameof(AppraisalReview), request.ReviewId);

        var callerEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        if (review.EmployeeId != callerEmployeeId) throw new ForbiddenException("You can only submit your own self-review.");
        if (review.Stage != AppraisalStage.SelfReview) throw new ConflictException("This review is not awaiting a self-review submission.");

        review.SelfAnswersJson = request.AnswersJson;
        review.SelfSubmittedAtUtc = DateTime.UtcNow;
        review.Stage = AppraisalStage.ManagerReview;
        await _db.SaveChangesAsync(ct);
    }
}

public class SubmitManagerReviewCommandHandler : IRequestHandler<SubmitManagerReviewCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly IPermissionChecker _permissionChecker;
    public SubmitManagerReviewCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver, IPermissionChecker permissionChecker)
    { _db = db; _employeeResolver = employeeResolver; _permissionChecker = permissionChecker; }

    public async Task Handle(SubmitManagerReviewCommand request, CancellationToken ct)
    {
        var review = await _db.AppraisalReviews.FirstOrDefaultAsync(r => r.Id == request.ReviewId, ct)
            ?? throw new NotFoundException(nameof(AppraisalReview), request.ReviewId);

        var callerEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var isElevated = _permissionChecker.HasPermission(Permissions.ReportRead);
        if (!isElevated && review.ManagerId != callerEmployeeId)
            throw new ForbiddenException("You can only submit a manager review for your own direct reports.");
        if (review.Stage != AppraisalStage.ManagerReview) throw new ConflictException("This review is not awaiting a manager-review submission.");

        review.ManagerAnswersJson = request.AnswersJson;
        review.ManagerSubmittedAtUtc = DateTime.UtcNow;
        review.Stage = AppraisalStage.Calibration;
        await _db.SaveChangesAsync(ct);
    }
}

/// <summary>Calibration is optional per the spec ("self-review → manager review → optional calibration →
/// final rating") — this can finalize directly from either ManagerReview or Calibration stage.</summary>
public class FinalizeAppraisalReviewCommandHandler : IRequestHandler<FinalizeAppraisalReviewCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly IPermissionChecker _permissionChecker;
    public FinalizeAppraisalReviewCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver, IPermissionChecker permissionChecker)
    { _db = db; _employeeResolver = employeeResolver; _permissionChecker = permissionChecker; }

    public async Task Handle(FinalizeAppraisalReviewCommand request, CancellationToken ct)
    {
        var review = await _db.AppraisalReviews.FirstOrDefaultAsync(r => r.Id == request.ReviewId, ct)
            ?? throw new NotFoundException(nameof(AppraisalReview), request.ReviewId);

        var callerEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var isElevated = _permissionChecker.HasPermission(Permissions.ReportRead);
        if (!isElevated && review.ManagerId != callerEmployeeId)
            throw new ForbiddenException("You can only finalize a review for your own direct reports.");
        if (review.Stage is not (AppraisalStage.ManagerReview or AppraisalStage.Calibration))
            throw new ConflictException("This review has not completed manager review yet.");

        review.FinalRating = request.FinalRating;
        review.FinalComments = request.FinalComments;
        review.CalibrationNotes = request.CalibrationNotes;
        review.Stage = AppraisalStage.Completed;
        review.CompletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetMyAppraisalReviewsQueryHandler : IRequestHandler<GetMyAppraisalReviewsQuery, IReadOnlyList<AppraisalReviewDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public GetMyAppraisalReviewsQueryHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task<IReadOnlyList<AppraisalReviewDto>> Handle(GetMyAppraisalReviewsQuery request, CancellationToken ct)
    {
        var callerEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        return await _db.AppraisalReviews.Where(r => r.EmployeeId == callerEmployeeId)
            .Select(r => new AppraisalReviewDto(r.Id, r.CycleId, r.EmployeeId, r.ManagerId, r.Stage, r.SelfAnswersJson, r.ManagerAnswersJson, r.FinalRating, r.FinalComments))
            .ToListAsync(ct);
    }
}

public class GetTeamAppraisalReviewsQueryHandler : IRequestHandler<GetTeamAppraisalReviewsQuery, IReadOnlyList<AppraisalReviewDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly IPermissionChecker _permissionChecker;
    public GetTeamAppraisalReviewsQueryHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver, IPermissionChecker permissionChecker)
    { _db = db; _employeeResolver = employeeResolver; _permissionChecker = permissionChecker; }

    public async Task<IReadOnlyList<AppraisalReviewDto>> Handle(GetTeamAppraisalReviewsQuery request, CancellationToken ct)
    {
        var callerEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var isElevated = _permissionChecker.HasPermission(Permissions.ReportRead);

        var query = _db.AppraisalReviews.Where(r => r.CycleId == request.CycleId);
        query = isElevated ? query : query.Where(r => r.ManagerId == callerEmployeeId);

        return await query
            .Select(r => new AppraisalReviewDto(r.Id, r.CycleId, r.EmployeeId, r.ManagerId, r.Stage, r.SelfAnswersJson, r.ManagerAnswersJson, r.FinalRating, r.FinalComments))
            .ToListAsync(ct);
    }
}
