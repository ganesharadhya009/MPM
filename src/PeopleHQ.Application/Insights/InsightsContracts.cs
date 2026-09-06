using MediatR;

namespace PeopleHQ.Application.Insights;

// Phase 5 "Differentiation / AI-assisted" (05-enhancements-and-roadmap.md). Both queries are manager/admin
// -facing signals ONLY — gated by attritionrisk.read, never exposed to the employee themselves, and never
// wired to any automated action (no auto-triggered workflow, no notification to the employee). Purely
// informational, opt-out in spirit (a tenant simply doesn't have to look at them).

public record GetAttritionRiskScoreQuery(Guid EmployeeId) : IRequest<AttritionRiskDto>;
public record AttritionRiskDto(
    Guid EmployeeId, decimal TenureMonths, decimal BradfordScore, decimal? EngagementTrendScore,
    int ManagerChangeCount, decimal RiskScore, string RiskLevel);

public record GetTeamAttritionRiskQuery(Guid ManagerId) : IRequest<IReadOnlyList<AttritionRiskDto>>;

public record GetAttendanceAnomalyInsightsQuery(Guid EmployeeId, int Year) : IRequest<AttendanceAnomalyDto>;
public record AttendanceAnomalyDto(
    Guid EmployeeId, int Year, int TotalAbsences, int MondayFridayAbsences, decimal MondayFridayAbsenceRatio,
    int ShortNoticeLeaveCount, bool HasMondayFridayPattern, bool HasFrequentShortNoticeLeave);
