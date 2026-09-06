using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Insights;
using PeopleHQ.Application.Leave;
using PeopleHQ.Domain.Attendance;
using PeopleHQ.Domain.Employees;
using PeopleHQ.Domain.Leave;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Insights;

/// <summary>
/// Attrition-risk scoring (05-enhancements-and-roadmap.md Phase 5): "combining tenure, Bradford score,
/// engagement survey trend, and manager-change frequency — presented as a manager-facing signal, never an
/// automated action." The 0-100 RiskScore below is a deliberately simple, documented heuristic — not a
/// validated statistical model — weighted so an unknown/unavailable input (no engagement data, no tracked
/// manager changes) contributes zero rather than inflating the score. Reruns of the formula are expected as
/// real usage data accumulates; nothing here is wired to any automated action per the spec's own framing.
/// </summary>
public class GetAttritionRiskScoreQueryHandler : IRequestHandler<GetAttritionRiskScoreQuery, AttritionRiskDto>
{
    private readonly AppDbContext _db;
    private readonly ISender _sender;
    public GetAttritionRiskScoreQueryHandler(AppDbContext db, ISender sender) { _db = db; _sender = sender; }

    public async Task<AttritionRiskDto> Handle(GetAttritionRiskScoreQuery request, CancellationToken ct)
        => await ComputeAsync(_db, _sender, request.EmployeeId, ct);

    internal static async Task<AttritionRiskDto> ComputeAsync(AppDbContext db, ISender sender, Guid employeeId, CancellationToken ct)
    {
        var employee = await db.Employees.FindAsync(new object[] { employeeId }, ct)
            ?? throw new NotFoundException(nameof(Employee), employeeId);

        var tenureMonths = Math.Round((decimal)(DateTime.UtcNow.Date - employee.JoinDate.ToDateTime(TimeOnly.MinValue)).TotalDays / 30.44m, 1);

        var bradford = await sender.Send(new GetBradfordScoreQuery(employeeId, DateTime.UtcNow.Year), ct);

        // Only counts responses to NON-anonymous surveys the employee personally answered — anonymous
        // surveys (the Survey.IsAnonymous default) never record RespondentEmployeeId, so per-employee
        // engagement trend is genuinely unavailable for most orgs in practice; null here means "unknown",
        // not "neutral", and is weighted out of the score below rather than treated as a middling score.
        var ownResponses = await db.SurveyResponses
            .Where(r => r.RespondentEmployeeId == employeeId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(10)
            .Select(r => r.Score)
            .ToListAsync(ct);
        decimal? engagementTrendScore = ownResponses.Count > 0 ? Math.Round((decimal)ownResponses.Average(), 1) : null;

        // EmployeePositionHistory is modeled (02-data-model-erd.md) but no handler in this codebase writes
        // it yet — a documented, pre-existing gap. This always evaluates to 0 until that gap is closed, at
        // which point this query benefits automatically with no further changes needed.
        var managerChangeCount = await db.EmployeePositionHistories.CountAsync(h => h.EmployeeId == employeeId, ct);

        var bradfordFactor = Math.Min(bradford.Score / 500m, 1m) * 40m;
        var tenureFactor = tenureMonths switch
        {
            < 6m => 20m,
            < 24m => 20m * (24m - tenureMonths) / 18m,
            _ => 0m
        };
        var engagementFactor = engagementTrendScore is null ? 0m : Math.Clamp((10m - engagementTrendScore.Value) / 10m * 25m, 0m, 25m);
        var managerChangeFactor = Math.Min(managerChangeCount * 5m, 15m);

        var riskScore = Math.Clamp(bradfordFactor + tenureFactor + engagementFactor + managerChangeFactor, 0m, 100m);
        var riskLevel = riskScore switch { < 33m => "Low", < 66m => "Medium", _ => "High" };

        return new AttritionRiskDto(employeeId, tenureMonths, bradford.Score, engagementTrendScore, managerChangeCount, Math.Round(riskScore, 1), riskLevel);
    }
}

public class GetTeamAttritionRiskQueryHandler : IRequestHandler<GetTeamAttritionRiskQuery, IReadOnlyList<AttritionRiskDto>>
{
    private readonly AppDbContext _db;
    private readonly ISender _sender;
    public GetTeamAttritionRiskQueryHandler(AppDbContext db, ISender sender) { _db = db; _sender = sender; }

    public async Task<IReadOnlyList<AttritionRiskDto>> Handle(GetTeamAttritionRiskQuery request, CancellationToken ct)
    {
        var reporteeIds = await _db.Employees.Where(e => e.ManagerId == request.ManagerId).Select(e => e.Id).ToListAsync(ct);
        var results = new List<AttritionRiskDto>();
        foreach (var employeeId in reporteeIds)
            results.Add(await GetAttritionRiskScoreQueryHandler.ComputeAsync(_db, _sender, employeeId, ct));
        return results;
    }
}

/// <summary>
/// Attendance/leave anomaly insights (05-enhancements-and-roadmap.md Phase 5): "already seeded conceptually
/// in Phase 1 (Bradford score) — extend with pattern-detection." Two simple, documented heuristics beyond
/// Bradford: a Monday/Friday absence clustering ratio (suggesting extended-weekend patterns) and short-notice
/// leave frequency (leave requested with ≤1 day's notice). Thresholds below are illustrative constants, not
/// tenant-configurable in this pass — a documented follow-up, not a defect.
/// </summary>
public class GetAttendanceAnomalyInsightsQueryHandler : IRequestHandler<GetAttendanceAnomalyInsightsQuery, AttendanceAnomalyDto>
{
    private readonly AppDbContext _db;
    public GetAttendanceAnomalyInsightsQueryHandler(AppDbContext db) => _db = db;

    public async Task<AttendanceAnomalyDto> Handle(GetAttendanceAnomalyInsightsQuery request, CancellationToken ct)
    {
        var absences = await _db.AttendanceRecords
            .Where(r => r.EmployeeId == request.EmployeeId && r.Status == AttendanceStatus.Absent && r.Date.Year == request.Year)
            .Select(r => r.Date)
            .ToListAsync(ct);

        var mondayFridayCount = absences.Count(d => d.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Friday);
        var ratio = absences.Count > 0 ? Math.Round((decimal)mondayFridayCount / absences.Count, 2) : 0m;

        var approvedLeaveRequests = await _db.LeaveRequests
            .Where(r => r.EmployeeId == request.EmployeeId && r.Status == LeaveRequestStatus.Approved && r.StartDate.Year == request.Year)
            .Select(r => new { r.StartDate, r.CreatedAtUtc })
            .ToListAsync(ct);
        var shortNoticeCount = approvedLeaveRequests.Count(r => (r.StartDate.DayNumber - DateOnly.FromDateTime(r.CreatedAtUtc).DayNumber) <= 1);

        return new AttendanceAnomalyDto(
            request.EmployeeId, request.Year, absences.Count, mondayFridayCount, ratio, shortNoticeCount,
            HasMondayFridayPattern: absences.Count >= 3 && ratio > 0.5m,
            HasFrequentShortNoticeLeave: shortNoticeCount >= 3);
    }
}
