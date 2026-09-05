using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Reports;
using PeopleHQ.Domain.Employees;
using PeopleHQ.Domain.Onboarding;
using PeopleHQ.Domain.Workflow;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Reports;

public class GetHeadcountReportQueryHandler : IRequestHandler<GetHeadcountReportQuery, HeadcountReportDto>
{
    private readonly AppDbContext _db;
    public GetHeadcountReportQueryHandler(AppDbContext db) => _db = db;

    public async Task<HeadcountReportDto> Handle(GetHeadcountReportQuery request, CancellationToken ct)
    {
        var active = _db.Employees.Where(e => e.Status == EmployeeStatus.Active);

        var byDepartment = await active
            .GroupBy(e => new { e.DepartmentId, DeptName = e.DepartmentId == null ? null : _db.Departments.Where(d => d.Id == e.DepartmentId).Select(d => d.Name).FirstOrDefault() })
            .Select(g => new HeadcountBreakdownRow(g.Key.DepartmentId, g.Key.DeptName ?? "Unassigned", g.Count()))
            .ToListAsync(ct);

        var byLocation = await active
            .GroupBy(e => new { e.LocationId, LocName = e.LocationId == null ? null : _db.Locations.Where(l => l.Id == e.LocationId).Select(l => l.Name).FirstOrDefault() })
            .Select(g => new HeadcountBreakdownRow(g.Key.LocationId, g.Key.LocName ?? "Unassigned", g.Count()))
            .ToListAsync(ct);

        var byDesignation = await active
            .GroupBy(e => new { e.DesignationId, DesigTitle = e.DesignationId == null ? null : _db.Designations.Where(d => d.Id == e.DesignationId).Select(d => d.Title).FirstOrDefault() })
            .Select(g => new HeadcountBreakdownRow(g.Key.DesignationId, g.Key.DesigTitle ?? "Unassigned", g.Count()))
            .ToListAsync(ct);

        var totalActive = await active.CountAsync(ct);
        return new HeadcountReportDto(totalActive, byDepartment, byLocation, byDesignation);
    }
}

public class GetAttritionReportQueryHandler : IRequestHandler<GetAttritionReportQuery, AttritionReportDto>
{
    private readonly AppDbContext _db;
    public GetAttritionReportQueryHandler(AppDbContext db) => _db = db;

    public async Task<AttritionReportDto> Handle(GetAttritionReportQuery request, CancellationToken ct)
    {
        var exitedCount = await _db.Employees.CountAsync(e =>
            e.Status == EmployeeStatus.Exited && e.ExitDate != null &&
            e.ExitDate >= request.StartDate && e.ExitDate <= request.EndDate, ct);

        var headcountAtStart = await HeadcountAsOfAsync(request.StartDate, ct);
        var headcountAtEnd = await HeadcountAsOfAsync(request.EndDate, ct);
        var averageHeadcount = (headcountAtStart + headcountAtEnd) / 2m;

        var attritionRate = averageHeadcount > 0 ? Math.Round(exitedCount / averageHeadcount * 100m, 2) : 0m;
        return new AttritionReportDto(exitedCount, averageHeadcount, attritionRate);
    }

    private async Task<int> HeadcountAsOfAsync(DateOnly asOf, CancellationToken ct)
        => await _db.Employees.CountAsync(e => e.JoinDate <= asOf && (e.ExitDate == null || e.ExitDate > asOf), ct);
}

public class GetLeaveUtilizationReportQueryHandler : IRequestHandler<GetLeaveUtilizationReportQuery, IReadOnlyList<LeaveUtilizationRowDto>>
{
    private readonly AppDbContext _db;
    public GetLeaveUtilizationReportQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<LeaveUtilizationRowDto>> Handle(GetLeaveUtilizationReportQuery request, CancellationToken ct)
    {
        var rows = await _db.LeaveBalances
            .Where(b => b.Year == request.Year)
            .GroupBy(b => b.LeaveTypeId)
            .Select(g => new { LeaveTypeId = g.Key, TotalAccrued = g.Sum(b => b.Accrued), TotalUsed = g.Sum(b => b.Used) })
            .ToListAsync(ct);

        var leaveTypeNames = await _db.LeaveTypes.ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        return rows.Select(r => new LeaveUtilizationRowDto(
            r.LeaveTypeId,
            leaveTypeNames.TryGetValue(r.LeaveTypeId, out var name) ? name : "Unknown",
            r.TotalAccrued, r.TotalUsed,
            r.TotalAccrued > 0 ? Math.Round(r.TotalUsed / r.TotalAccrued * 100m, 2) : 0m)).ToList();
    }
}

public class GetAttendanceSummaryReportQueryHandler : IRequestHandler<GetAttendanceSummaryReportQuery, IReadOnlyList<AttendanceSummaryRowDto>>
{
    private readonly AppDbContext _db;
    public GetAttendanceSummaryReportQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<AttendanceSummaryRowDto>> Handle(GetAttendanceSummaryReportQuery request, CancellationToken ct)
    {
        var rows = await _db.AttendanceRecords
            .Where(r => r.Date >= request.StartDate && r.Date <= request.EndDate)
            .GroupBy(r => r.EmployeeId)
            .Select(g => new
            {
                EmployeeId = g.Key,
                PresentDays = g.Count(r => r.Status == Domain.Attendance.AttendanceStatus.Present),
                AbsentDays = g.Count(r => r.Status == Domain.Attendance.AttendanceStatus.Absent),
                HalfDays = g.Count(r => r.Status == Domain.Attendance.AttendanceStatus.HalfDay),
                OnLeaveDays = g.Count(r => r.Status == Domain.Attendance.AttendanceStatus.OnLeave),
                TotalOvertimeHours = g.Sum(r => r.OvertimeHours)
            })
            .ToListAsync(ct);

        var employeeIds = rows.Select(r => r.EmployeeId).ToList();
        var employees = await _db.Employees.Where(e => employeeIds.Contains(e.Id))
            .Select(e => new { e.Id, e.EmployeeCode, e.FirstName, e.LastName }).ToDictionaryAsync(e => e.Id, ct);

        return rows.Select(r =>
        {
            employees.TryGetValue(r.EmployeeId, out var employee);
            return new AttendanceSummaryRowDto(
                r.EmployeeId, employee?.EmployeeCode ?? "Unknown", employee is null ? "Unknown" : $"{employee.FirstName} {employee.LastName}",
                r.PresentDays, r.AbsentDays, r.HalfDays, r.OnLeaveDays, r.TotalOvertimeHours);
        }).ToList();
    }
}

public class GetOnboardingTimeToProductivityReportQueryHandler : IRequestHandler<GetOnboardingTimeToProductivityReportQuery, IReadOnlyList<OnboardingTimeToProductivityRowDto>>
{
    private readonly AppDbContext _db;
    public GetOnboardingTimeToProductivityReportQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<OnboardingTimeToProductivityRowDto>> Handle(GetOnboardingTimeToProductivityReportQuery request, CancellationToken ct)
    {
        var tasks = await _db.OnboardingTasks.Where(t => t.EmployeeId != null).ToListAsync(ct);
        var employeeIds = tasks.Select(t => t.EmployeeId!.Value).Distinct().ToList();
        var employees = await _db.Employees.Where(e => employeeIds.Contains(e.Id))
            .Select(e => new { e.Id, e.EmployeeCode, e.JoinDate }).ToDictionaryAsync(e => e.Id, ct);

        var result = new List<OnboardingTimeToProductivityRowDto>();
        foreach (var group in tasks.GroupBy(t => t.EmployeeId!.Value))
        {
            if (!employees.TryGetValue(group.Key, out var employee)) continue;
            var total = group.Count();
            var doneTasks = group.Where(t => t.Status == OnboardingTaskStatus.Done).ToList();
            int? daysToComplete = null;
            if (doneTasks.Count == total && total > 0)
                daysToComplete = (DateOnly.FromDateTime(doneTasks.Max(t => t.UpdatedAtUtc)).DayNumber - employee.JoinDate.DayNumber);

            result.Add(new OnboardingTimeToProductivityRowDto(group.Key, employee.EmployeeCode, employee.JoinDate, total, doneTasks.Count, daysToComplete));
        }
        return result;
    }
}

public class GetApprovalSlaReportQueryHandler : IRequestHandler<GetApprovalSlaReportQuery, IReadOnlyList<ApprovalSlaRowDto>>
{
    private readonly AppDbContext _db;
    public GetApprovalSlaReportQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ApprovalSlaRowDto>> Handle(GetApprovalSlaReportQuery request, CancellationToken ct)
    {
        var startUtc = request.StartDate.ToDateTime(TimeOnly.MinValue);
        var endUtc = request.EndDate.ToDateTime(TimeOnly.MaxValue);

        var resolved = await _db.WorkflowRequests
            .Where(r => r.SubmittedAtUtc != null && r.ResolvedAtUtc != null && r.SubmittedAtUtc >= startUtc && r.SubmittedAtUtc <= endUtc)
            .Select(r => new { r.RequestType, r.SubmittedAtUtc, r.ResolvedAtUtc })
            .ToListAsync(ct);

        return resolved
            .GroupBy(r => r.RequestType)
            .Select(g => new ApprovalSlaRowDto(
                g.Key, g.Count(),
                Math.Round((decimal)g.Average(r => (r.ResolvedAtUtc!.Value - r.SubmittedAtUtc!.Value).TotalHours), 2)))
            .ToList();
    }
}
