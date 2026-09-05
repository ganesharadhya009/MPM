using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.Engagement;
using PeopleHQ.Domain.Engagement;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Engagement;

public class CreateSurveyCommandHandler : IRequestHandler<CreateSurveyCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public CreateSurveyCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<Guid> Handle(CreateSurveyCommand request, CancellationToken ct)
    {
        var survey = new Survey { TenantId = _tenant.TenantId, Question = request.Question, IsAnonymous = request.IsAnonymous, IsActive = true };
        _db.Surveys.Add(survey);
        await _db.SaveChangesAsync(ct);
        return survey.Id;
    }
}

public class DeactivateSurveyCommandHandler : IRequestHandler<DeactivateSurveyCommand>
{
    private readonly AppDbContext _db;
    public DeactivateSurveyCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(DeactivateSurveyCommand request, CancellationToken ct)
    {
        var survey = await _db.Surveys.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Survey), request.Id);
        survey.IsActive = false;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetActiveSurveysQueryHandler : IRequestHandler<GetActiveSurveysQuery, IReadOnlyList<SurveyDto>>
{
    private readonly AppDbContext _db;
    public GetActiveSurveysQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<SurveyDto>> Handle(GetActiveSurveysQuery request, CancellationToken ct)
        => await _db.Surveys.Where(s => s.IsActive)
            .Select(s => new SurveyDto(s.Id, s.Question, s.IsAnonymous, s.IsActive))
            .ToListAsync(ct);
}

public class SubmitSurveyResponseCommandHandler : IRequestHandler<SubmitSurveyResponseCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public SubmitSurveyResponseCommandHandler(AppDbContext db, ITenantContext tenant, ICurrentEmployeeResolver employeeResolver)
    { _db = db; _tenant = tenant; _employeeResolver = employeeResolver; }

    public async Task<Guid> Handle(SubmitSurveyResponseCommand request, CancellationToken ct)
    {
        var survey = await _db.Surveys.FindAsync(new object[] { request.SurveyId }, ct) ?? throw new NotFoundException(nameof(Survey), request.SurveyId);
        if (!survey.IsActive) throw new ConflictException("This survey is no longer active.");
        if (request.Score is < 0 or > 10) throw new ValidationException(nameof(request.Score), "Score must be between 0 and 10.");

        // True anonymity per the domain model: only capture the respondent when the survey is not anonymous.
        Guid? respondentEmployeeId = survey.IsAnonymous ? null : await _employeeResolver.GetCurrentEmployeeIdAsync(ct);

        var response = new SurveyResponse
        {
            TenantId = _tenant.TenantId,
            SurveyId = request.SurveyId,
            RespondentEmployeeId = respondentEmployeeId,
            Score = request.Score,
            Comment = request.Comment
        };
        _db.SurveyResponses.Add(response);
        await _db.SaveChangesAsync(ct);
        return response.Id;
    }
}

public class GetSurveyResultsQueryHandler : IRequestHandler<GetSurveyResultsQuery, SurveyResultsDto>
{
    private readonly AppDbContext _db;
    public GetSurveyResultsQueryHandler(AppDbContext db) => _db = db;

    public async Task<SurveyResultsDto> Handle(GetSurveyResultsQuery request, CancellationToken ct)
    {
        var scores = await _db.SurveyResponses.Where(r => r.SurveyId == request.SurveyId).Select(r => r.Score).ToListAsync(ct);
        if (scores.Count == 0) return new SurveyResultsDto(request.SurveyId, 0, 0m, 0, 0, 0, null);

        var promoters = scores.Count(s => s >= 9);
        var detractors = scores.Count(s => s <= 6);
        var passives = scores.Count - promoters - detractors;
        var enps = Math.Round((decimal)(promoters - detractors) / scores.Count * 100m, 2);

        return new SurveyResultsDto(request.SurveyId, scores.Count, Math.Round((decimal)scores.Average(), 2), promoters, passives, detractors, enps);
    }
}

public class CreateAssetCommandHandler : IRequestHandler<CreateAssetCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public CreateAssetCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<Guid> Handle(CreateAssetCommand request, CancellationToken ct)
    {
        var asset = new Asset { TenantId = _tenant.TenantId, Name = request.Name, SerialNo = request.SerialNo, Status = AssetStatus.InStock };
        _db.Assets.Add(asset);
        await _db.SaveChangesAsync(ct);
        return asset.Id;
    }
}

public class AssignAssetCommandHandler : IRequestHandler<AssignAssetCommand>
{
    private readonly AppDbContext _db;
    public AssignAssetCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(AssignAssetCommand request, CancellationToken ct)
    {
        var asset = await _db.Assets.FindAsync(new object[] { request.AssetId }, ct) ?? throw new NotFoundException(nameof(Asset), request.AssetId);
        if (asset.Status == AssetStatus.Retired) throw new ConflictException("A retired asset cannot be assigned.");

        asset.AssignedEmployeeId = request.EmployeeId;
        asset.Status = AssetStatus.Assigned;
        await _db.SaveChangesAsync(ct);
    }
}

public class ReturnAssetCommandHandler : IRequestHandler<ReturnAssetCommand>
{
    private readonly AppDbContext _db;
    public ReturnAssetCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(ReturnAssetCommand request, CancellationToken ct)
    {
        var asset = await _db.Assets.FindAsync(new object[] { request.AssetId }, ct) ?? throw new NotFoundException(nameof(Asset), request.AssetId);
        asset.AssignedEmployeeId = null;
        asset.Status = AssetStatus.Returned;
        await _db.SaveChangesAsync(ct);
    }
}

public class RetireAssetCommandHandler : IRequestHandler<RetireAssetCommand>
{
    private readonly AppDbContext _db;
    public RetireAssetCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(RetireAssetCommand request, CancellationToken ct)
    {
        var asset = await _db.Assets.FindAsync(new object[] { request.AssetId }, ct) ?? throw new NotFoundException(nameof(Asset), request.AssetId);
        asset.AssignedEmployeeId = null;
        asset.Status = AssetStatus.Retired;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetAssetsQueryHandler : IRequestHandler<GetAssetsQuery, IReadOnlyList<AssetDto>>
{
    private readonly AppDbContext _db;
    public GetAssetsQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<AssetDto>> Handle(GetAssetsQuery request, CancellationToken ct)
    {
        var query = _db.Assets.AsQueryable();
        if (request.AssignedEmployeeId is not null) query = query.Where(a => a.AssignedEmployeeId == request.AssignedEmployeeId);
        if (request.Status is not null) query = query.Where(a => a.Status == request.Status);

        return await query.Select(a => new AssetDto(a.Id, a.Name, a.SerialNo, a.AssignedEmployeeId, a.Status)).ToListAsync(ct);
    }
}

public class CreateHelpdeskTicketCommandHandler : IRequestHandler<CreateHelpdeskTicketCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public CreateHelpdeskTicketCommandHandler(AppDbContext db, ITenantContext tenant, ICurrentEmployeeResolver employeeResolver)
    { _db = db; _tenant = tenant; _employeeResolver = employeeResolver; }

    public async Task<Guid> Handle(CreateHelpdeskTicketCommand request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var ticket = new HelpdeskTicket
        {
            TenantId = _tenant.TenantId,
            RaisedByEmployeeId = employeeId,
            Category = request.Category,
            Subject = request.Subject,
            Description = request.Description,
            Status = HelpdeskTicketStatus.Open
        };
        _db.HelpdeskTickets.Add(ticket);
        await _db.SaveChangesAsync(ct);
        return ticket.Id;
    }
}

public class AssignHelpdeskTicketCommandHandler : IRequestHandler<AssignHelpdeskTicketCommand>
{
    private readonly AppDbContext _db;
    public AssignHelpdeskTicketCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(AssignHelpdeskTicketCommand request, CancellationToken ct)
    {
        var ticket = await _db.HelpdeskTickets.FindAsync(new object[] { request.TicketId }, ct) ?? throw new NotFoundException(nameof(HelpdeskTicket), request.TicketId);
        ticket.AssignedToEmployeeId = request.AssignedToEmployeeId;
        ticket.SlaDueAtUtc = request.SlaDueAtUtc;
        if (ticket.Status == HelpdeskTicketStatus.Open) ticket.Status = HelpdeskTicketStatus.InProgress;
        await _db.SaveChangesAsync(ct);
    }
}

public class UpdateHelpdeskTicketStatusCommandHandler : IRequestHandler<UpdateHelpdeskTicketStatusCommand>
{
    private readonly AppDbContext _db;
    public UpdateHelpdeskTicketStatusCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(UpdateHelpdeskTicketStatusCommand request, CancellationToken ct)
    {
        var ticket = await _db.HelpdeskTickets.FindAsync(new object[] { request.TicketId }, ct) ?? throw new NotFoundException(nameof(HelpdeskTicket), request.TicketId);
        ticket.Status = request.Status;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetHelpdeskTicketsQueryHandler : IRequestHandler<GetHelpdeskTicketsQuery, IReadOnlyList<HelpdeskTicketDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly IPermissionChecker _permissionChecker;
    public GetHelpdeskTicketsQueryHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver, IPermissionChecker permissionChecker)
    { _db = db; _employeeResolver = employeeResolver; _permissionChecker = permissionChecker; }

    public async Task<IReadOnlyList<HelpdeskTicketDto>> Handle(GetHelpdeskTicketsQuery request, CancellationToken ct)
    {
        var callerId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var canManage = _permissionChecker.HasPermission(Domain.Identity.Permissions.HelpdeskTicketManage);
        if (!canManage && request.RaisedByEmployeeId is not null && request.RaisedByEmployeeId != callerId)
            throw new ForbiddenException("You can only view your own helpdesk tickets.");

        var effectiveRaisedBy = canManage ? request.RaisedByEmployeeId : callerId;

        var query = _db.HelpdeskTickets.AsQueryable();
        if (effectiveRaisedBy is not null) query = query.Where(t => t.RaisedByEmployeeId == effectiveRaisedBy);
        if (request.Status is not null) query = query.Where(t => t.Status == request.Status);

        return await query
            .Select(t => new HelpdeskTicketDto(t.Id, t.RaisedByEmployeeId, t.Category, t.Subject, t.Description, t.Status, t.AssignedToEmployeeId, t.SlaDueAtUtc))
            .ToListAsync(ct);
    }
}

public class CreateAnnouncementCommandHandler : IRequestHandler<CreateAnnouncementCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public CreateAnnouncementCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<Guid> Handle(CreateAnnouncementCommand request, CancellationToken ct)
    {
        var announcement = new Announcement
        {
            TenantId = _tenant.TenantId,
            Title = request.Title,
            Body = request.Body,
            AudienceJson = request.AudienceJson,
            PublishedAtUtc = DateTime.UtcNow
        };
        _db.Announcements.Add(announcement);
        await _db.SaveChangesAsync(ct);
        return announcement.Id;
    }
}

public class DeleteAnnouncementCommandHandler : IRequestHandler<DeleteAnnouncementCommand>
{
    private readonly AppDbContext _db;
    public DeleteAnnouncementCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(DeleteAnnouncementCommand request, CancellationToken ct)
    {
        var announcement = await _db.Announcements.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(Announcement), request.Id);
        announcement.IsDeleted = true;
        announcement.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetActiveAnnouncementsQueryHandler : IRequestHandler<GetActiveAnnouncementsQuery, IReadOnlyList<AnnouncementDto>>
{
    private readonly AppDbContext _db;
    public GetActiveAnnouncementsQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<AnnouncementDto>> Handle(GetActiveAnnouncementsQuery request, CancellationToken ct)
        => await _db.Announcements
            .Where(a => a.PublishedAtUtc != null)
            .OrderByDescending(a => a.PublishedAtUtc)
            .Select(a => new AnnouncementDto(a.Id, a.Title, a.Body, a.AudienceJson, a.PublishedAtUtc))
            .ToListAsync(ct);
}
