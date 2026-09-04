using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.Payroll;
using PeopleHQ.Domain.Attendance;
using PeopleHQ.Domain.Employees;
using PeopleHQ.Domain.Identity;
using PeopleHQ.Domain.Leave;
using PeopleHQ.Domain.Payroll;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Payroll;

// ===== Payroll Run lifecycle =====
public class CreatePayrollRunCommandHandler : IRequestHandler<CreatePayrollRunCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant;
    public CreatePayrollRunCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<Guid> Handle(CreatePayrollRunCommand request, CancellationToken ct)
    {
        if (request.PeriodMonth is < 1 or > 12) throw new ValidationException(nameof(request.PeriodMonth), "Month must be between 1 and 12.");
        var exists = await _db.PayrollRuns.AnyAsync(r => r.PeriodMonth == request.PeriodMonth && r.PeriodYear == request.PeriodYear, ct);
        if (exists) throw new ConflictException($"A payroll run for {request.PeriodMonth}/{request.PeriodYear} already exists.");

        var run = new PayrollRun { TenantId = _tenant.TenantId, PeriodMonth = request.PeriodMonth, PeriodYear = request.PeriodYear, Status = PayrollRunStatus.Draft };
        _db.PayrollRuns.Add(run);
        await _db.SaveChangesAsync(ct);
        return run.Id;
    }
}

/// <summary>The statutory-run computation: for every active employee with a salary assignment covering the
/// period, resolves gross/deductions from their salary snapshot, applies IStatutoryCalculator (PF/ESI/PT/TDS),
/// pro-rates for loss-of-pay days, and writes a PayrollRunItem + line-item breakdown. Idempotent while the run
/// is still Draft — re-running replaces each employee's item.</summary>
public class ComputePayrollRunCommandHandler : IRequestHandler<ComputePayrollRunCommand>
{
    private readonly AppDbContext _db;
    private readonly IStatutoryCalculator _calculator;
    public ComputePayrollRunCommandHandler(AppDbContext db, IStatutoryCalculator calculator) { _db = db; _calculator = calculator; }

    public async Task Handle(ComputePayrollRunCommand request, CancellationToken ct)
    {
        var run = await _db.PayrollRuns.FindAsync(new object[] { request.PayrollRunId }, ct) ?? throw new NotFoundException(nameof(PayrollRun), request.PayrollRunId);
        if (run.Status != PayrollRunStatus.Draft) throw new ConflictException("Only a Draft run can be computed.");

        var statutorySettings = await _db.StatutorySettings.FirstOrDefaultAsync(ct);
        var configJson = statutorySettings?.ConfigJson ?? "{}";
        var ptSlabInputs = await _db.PtSlabs.Select(s => new PtSlabInput(s.MinIncome, s.MaxIncome, s.TaxAmount)).ToListAsync(ct);

        var periodStart = new DateOnly(run.PeriodYear, run.PeriodMonth, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var financialYear = ComputeFinancialYear(run.PeriodYear, run.PeriodMonth);
        var daysInMonth = periodEnd.Day;

        var statutoryLineComponentIds = await _db.PayComponents
            .Where(c => new[] { "Employee PF", "Employee ESI", "Professional Tax", "TDS" }.Contains(c.Name))
            .ToDictionaryAsync(c => c.Name, c => c.Id, ct);

        var activeEmployees = await _db.Employees.Where(e => e.Status == EmployeeStatus.Active).ToListAsync(ct);
        foreach (var employee in activeEmployees)
        {
            var assignment = await _db.EmployeeSalaryAssignments
                .Where(a => a.EmployeeId == employee.Id && a.EffectiveFrom <= periodEnd && (a.EffectiveTo == null || a.EffectiveTo >= periodStart))
                .OrderByDescending(a => a.EffectiveFrom).FirstOrDefaultAsync(ct);
            if (assignment is null) continue; // no salary assigned for this period — skip rather than fail the whole run

            var componentValues = await (
                from v in _db.EmployeeSalaryComponentValues
                join pc in _db.PayComponents on v.PayComponentId equals pc.Id
                where v.AssignmentId == assignment.Id
                select new { v.PayComponentId, v.ComputedAmount, pc.ComponentType, pc.Name }
            ).ToListAsync(ct);

            var grossEarnings = componentValues.Where(c => c.ComponentType == PayComponentType.Earning).Sum(c => c.ComputedAmount);
            var basicEarnings = componentValues.Where(c => string.Equals(c.Name, "Basic", StringComparison.OrdinalIgnoreCase)).Sum(c => c.ComputedAmount);
            var flatDeductions = componentValues.Where(c => c.ComponentType == PayComponentType.Deduction).Sum(c => c.ComputedAmount);

            var lopDays = await _db.AttendanceRecords.CountAsync(a => a.EmployeeId == employee.Id && a.Date >= periodStart && a.Date <= periodEnd && a.Status == AttendanceStatus.Absent, ct);

            var verifiedDeclarations = await _db.InvestmentDeclarations
                .Where(d => d.EmployeeId == employee.Id && d.FinancialYear == financialYear && d.Status == DeclarationStatus.Verified)
                .SumAsync(d => (decimal?)d.DeclaredAmount, ct) ?? 0m;
            var taxRegime = await _db.EmployeeTaxRegimeSelections
                .Where(s => s.EmployeeId == employee.Id && s.FinancialYear == financialYear)
                .Select(s => s.Regime).FirstOrDefaultAsync(ct) ?? "New";

            var statutory = _calculator.Calculate(new StatutoryCalculationInput(
                grossEarnings, basicEarnings, assignment.CtcAnnual, verifiedDeclarations, taxRegime, configJson, ptSlabInputs));

            // Pro-rate earnings for loss-of-pay days; statutory contributions are left on the full computed
            // base (a simplification — real payroll typically pro-rates PF wage too, documented as a follow-up).
            var payableFraction = lopDays > 0 ? (decimal)(daysInMonth - lopDays) / daysInMonth : 1m;
            var adjustedGross = Math.Round(grossEarnings * payableFraction, 2);
            var totalDeductions = flatDeductions + statutory.EmployeePf + statutory.EmployeeEsi + statutory.ProfessionalTax + statutory.Tds;
            var netPay = adjustedGross - totalDeductions;

            var existingItem = await _db.PayrollRunItems.FirstOrDefaultAsync(i => i.PayrollRunId == run.Id && i.EmployeeId == employee.Id, ct);
            if (existingItem is not null)
            {
                var existingLines = await _db.PayrollRunItemLines.Where(l => l.PayrollRunItemId == existingItem.Id).ToListAsync(ct);
                _db.PayrollRunItemLines.RemoveRange(existingLines);
                _db.PayrollRunItems.Remove(existingItem);
                await _db.SaveChangesAsync(ct);
            }

            var item = new PayrollRunItem
            {
                PayrollRunId = run.Id, EmployeeId = employee.Id, GrossEarnings = adjustedGross, TotalDeductions = totalDeductions,
                NetPay = netPay, EmployerPf = statutory.EmployerPf, EmployerEsi = statutory.EmployerEsi, LopDays = lopDays,
                PaymentStatus = PaymentStatus.Pending
            };
            _db.PayrollRunItems.Add(item);
            await _db.SaveChangesAsync(ct); // need item.Id for lines

            foreach (var component in componentValues)
                _db.PayrollRunItemLines.Add(new PayrollRunItemLine { PayrollRunItemId = item.Id, PayComponentId = component.PayComponentId, Amount = component.ComputedAmount, IsManualOverride = false });

            // Best-effort traceability lines for the statutory deductions, only when the tenant has defined a
            // matching named PayComponent (e.g. "Employee PF") — otherwise they still count toward TotalDeductions
            // above but won't appear as a separate line.
            AddStatutoryLine(item.Id, "Employee PF", statutory.EmployeePf, statutoryLineComponentIds);
            AddStatutoryLine(item.Id, "Employee ESI", statutory.EmployeeEsi, statutoryLineComponentIds);
            AddStatutoryLine(item.Id, "Professional Tax", statutory.ProfessionalTax, statutoryLineComponentIds);
            AddStatutoryLine(item.Id, "TDS", statutory.Tds, statutoryLineComponentIds);

            await _db.SaveChangesAsync(ct);
        }

        run.Status = PayrollRunStatus.Computed;
        await _db.SaveChangesAsync(ct);
    }

    private void AddStatutoryLine(Guid itemId, string componentName, decimal amount, Dictionary<string, Guid> statutoryLineComponentIds)
    {
        if (amount <= 0 || !statutoryLineComponentIds.TryGetValue(componentName, out var payComponentId)) return;
        _db.PayrollRunItemLines.Add(new PayrollRunItemLine { PayrollRunItemId = itemId, PayComponentId = payComponentId, Amount = amount, IsManualOverride = false });
    }

    /// <summary>India financial year runs April-March.</summary>
    private static string ComputeFinancialYear(int year, int month)
        => month >= 4 ? $"{year}-{(year + 1) % 100:D2}" : $"{year - 1}-{year % 100:D2}";
}

public class OverridePayrollRunItemLineCommandHandler : IRequestHandler<OverridePayrollRunItemLineCommand>
{
    private readonly AppDbContext _db; private readonly ICurrentUserService _currentUser;
    public OverridePayrollRunItemLineCommandHandler(AppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task Handle(OverridePayrollRunItemLineCommand request, CancellationToken ct)
    {
        var item = await _db.PayrollRunItems.FindAsync(new object[] { request.PayrollRunItemId }, ct) ?? throw new NotFoundException(nameof(PayrollRunItem), request.PayrollRunItemId);
        var run = await _db.PayrollRuns.FindAsync(new object[] { item.PayrollRunId }, ct);
        if (run?.Status is not (PayrollRunStatus.Draft or PayrollRunStatus.Computed))
            throw new ConflictException("Line items can only be overridden before the run enters approval.");

        var payComponent = await _db.PayComponents.FindAsync(new object[] { request.PayComponentId }, ct) ?? throw new NotFoundException(nameof(PayComponent), request.PayComponentId);
        var line = await _db.PayrollRunItemLines.FirstOrDefaultAsync(l => l.PayrollRunItemId == item.Id && l.PayComponentId == request.PayComponentId, ct);
        var previousAmount = line?.Amount ?? 0m;

        if (line is null)
        {
            line = new PayrollRunItemLine { PayrollRunItemId = item.Id, PayComponentId = request.PayComponentId };
            _db.PayrollRunItemLines.Add(line);
        }
        line.Amount = request.Amount;
        line.IsManualOverride = true;

        var delta = request.Amount - previousAmount;
        if (payComponent.ComponentType == PayComponentType.Earning) { item.GrossEarnings += delta; item.NetPay += delta; }
        else { item.TotalDeductions += delta; item.NetPay -= delta; }
        item.OverriddenBy = _currentUser.UserId;
        item.OverrideReason = request.OverrideReason;

        await _db.SaveChangesAsync(ct);
    }
}

public class SubmitPayrollRunForApprovalCommandHandler : IRequestHandler<SubmitPayrollRunForApprovalCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly Application.Workflow.IWorkflowEngine _workflowEngine;

    public SubmitPayrollRunForApprovalCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver, Application.Workflow.IWorkflowEngine workflowEngine)
    { _db = db; _employeeResolver = employeeResolver; _workflowEngine = workflowEngine; }

    public async Task Handle(SubmitPayrollRunForApprovalCommand request, CancellationToken ct)
    {
        var run = await _db.PayrollRuns.FindAsync(new object[] { request.PayrollRunId }, ct) ?? throw new NotFoundException(nameof(PayrollRun), request.PayrollRunId);
        if (run.Status != PayrollRunStatus.Computed) throw new ConflictException("Only a Computed run can be submitted for approval.");

        var requesterEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        run.Status = PayrollRunStatus.PendingApproval;
        run.WorkflowRequestId = await _workflowEngine.SubmitAsync(Domain.Workflow.WorkflowRequestType.PayrollRunApproval, requesterEmployeeId,
            new { run.Id, run.PeriodMonth, run.PeriodYear }, ct);
        await _db.SaveChangesAsync(ct);
    }
}

public class LockPayrollRunCommandHandler : IRequestHandler<LockPayrollRunCommand>
{
    private readonly AppDbContext _db;
    public LockPayrollRunCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(LockPayrollRunCommand request, CancellationToken ct)
    {
        var run = await _db.PayrollRuns.FindAsync(new object[] { request.PayrollRunId }, ct) ?? throw new NotFoundException(nameof(PayrollRun), request.PayrollRunId);
        if (run.Status != PayrollRunStatus.Approved) throw new ConflictException("Only an Approved run can be locked.");
        run.Status = PayrollRunStatus.Locked;
        run.LockedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class MarkPayrollRunPaidCommandHandler : IRequestHandler<MarkPayrollRunPaidCommand>
{
    private readonly AppDbContext _db;
    public MarkPayrollRunPaidCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(MarkPayrollRunPaidCommand request, CancellationToken ct)
    {
        var run = await _db.PayrollRuns.FindAsync(new object[] { request.PayrollRunId }, ct) ?? throw new NotFoundException(nameof(PayrollRun), request.PayrollRunId);
        if (run.Status != PayrollRunStatus.Locked) throw new ConflictException("Only a Locked run can be marked paid.");

        var items = await _db.PayrollRunItems.Where(i => i.PayrollRunId == run.Id).ToListAsync(ct);
        foreach (var item in items) item.PaymentStatus = PaymentStatus.Paid;

        run.Status = PayrollRunStatus.Paid;
        run.PaidAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetPayrollRunsQueryHandler : IRequestHandler<GetPayrollRunsQuery, IReadOnlyList<PayrollRunSummaryDto>>
{
    private readonly AppDbContext _db;
    public GetPayrollRunsQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PayrollRunSummaryDto>> Handle(GetPayrollRunsQuery request, CancellationToken ct)
    {
        var query = _db.PayrollRuns.AsQueryable();
        if (request.PeriodYear is not null) query = query.Where(r => r.PeriodYear == request.PeriodYear);
        var runs = await query.OrderByDescending(r => r.PeriodYear).ThenByDescending(r => r.PeriodMonth).ToListAsync(ct);

        var runIds = runs.Select(r => r.Id).ToList();
        var itemsByRun = (await _db.PayrollRunItems.Where(i => runIds.Contains(i.PayrollRunId)).ToListAsync(ct)).ToLookup(i => i.PayrollRunId);

        return runs.Select(r => new PayrollRunSummaryDto(r.Id, r.PeriodMonth, r.PeriodYear, r.Status, r.WorkflowRequestId,
            itemsByRun[r.Id].Count(), itemsByRun[r.Id].Sum(i => i.NetPay))).ToList();
    }
}

public class GetPayrollRunItemsQueryHandler : IRequestHandler<GetPayrollRunItemsQuery, IReadOnlyList<PayrollRunItemDto>>
{
    private readonly AppDbContext _db;
    public GetPayrollRunItemsQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PayrollRunItemDto>> Handle(GetPayrollRunItemsQuery request, CancellationToken ct)
    {
        var items = await _db.PayrollRunItems.Where(i => i.PayrollRunId == request.PayrollRunId).ToListAsync(ct);
        var itemIds = items.Select(i => i.Id).ToList();
        var linesByItem = (await _db.PayrollRunItemLines.Where(l => itemIds.Contains(l.PayrollRunItemId)).ToListAsync(ct)).ToLookup(l => l.PayrollRunItemId);

        return items.Select(i => new PayrollRunItemDto(i.Id, i.EmployeeId, i.GrossEarnings, i.TotalDeductions, i.NetPay, i.EmployerPf, i.EmployerEsi, i.LopDays, i.PaymentStatus,
            linesByItem[i.Id].Select(l => new PayrollRunItemLineDto(l.PayComponentId, l.Amount, l.IsManualOverride)).ToList())).ToList();
    }
}

/// <summary>Approving a PayrollRunApproval request moves the run to Approved; rejecting reopens it to Computed
/// (adjustable and resubmittable) — the module-owned side effect the generic engine deliberately doesn't know about.</summary>
public class PayrollRunResolvedHandler : INotificationHandler<Application.Workflow.WorkflowRequestResolvedNotification>
{
    private readonly AppDbContext _db;
    public PayrollRunResolvedHandler(AppDbContext db) => _db = db;

    public async Task Handle(Application.Workflow.WorkflowRequestResolvedNotification notification, CancellationToken ct)
    {
        if (notification.RequestType != Domain.Workflow.WorkflowRequestType.PayrollRunApproval) return;
        var run = await _db.PayrollRuns.FirstOrDefaultAsync(r => r.WorkflowRequestId == notification.WorkflowRequestId, ct);
        if (run is null) return;

        if (notification.Status == Domain.Workflow.WorkflowStatus.Approved)
        {
            run.Status = PayrollRunStatus.Approved;
        }
        else if (notification.Status is Domain.Workflow.WorkflowStatus.Rejected or Domain.Workflow.WorkflowStatus.Withdrawn)
        {
            run.Status = PayrollRunStatus.Computed;
            run.WorkflowRequestId = null;
        }
        await _db.SaveChangesAsync(ct);
    }
}

// ===== Payslips =====
public class GeneratePayslipsCommandHandler : IRequestHandler<GeneratePayslipsCommand>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant;
    public GeneratePayslipsCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task Handle(GeneratePayslipsCommand request, CancellationToken ct)
    {
        var run = await _db.PayrollRuns.FindAsync(new object[] { request.PayrollRunId }, ct) ?? throw new NotFoundException(nameof(PayrollRun), request.PayrollRunId);
        if (run.Status is not (PayrollRunStatus.Locked or PayrollRunStatus.Paid)) throw new ConflictException("Payslips can only be generated for a Locked or Paid run.");

        var financialYear = ComputeFinancialYear(run.PeriodYear, run.PeriodMonth);
        var fyStartYear = int.Parse(financialYear.Split('-')[0]);
        var fyStart = new DateOnly(fyStartYear, 4, 1);

        var items = await _db.PayrollRunItems.Where(i => i.PayrollRunId == run.Id).ToListAsync(ct);
        foreach (var item in items)
        {
            var alreadyExists = await _db.Payslips.AnyAsync(p => p.EmployeeId == item.EmployeeId && p.PayrollRunItemId == item.Id, ct);
            if (alreadyExists) continue;

            var priorItemIds = await (
                from p in _db.Payslips
                join i in _db.PayrollRunItems on p.PayrollRunItemId equals i.Id
                join r in _db.PayrollRuns on i.PayrollRunId equals r.Id
                where p.EmployeeId == item.EmployeeId && r.PeriodYear >= fyStartYear
                      && new DateOnly(r.PeriodYear, r.PeriodMonth, 1) >= fyStart && new DateOnly(r.PeriodYear, r.PeriodMonth, 1) < fyStart.AddYears(1)
                select new { i.GrossEarnings, TdsLine = _db.PayrollRunItemLines.Where(l => l.PayrollRunItemId == i.Id && l.PayComponentId == _db.PayComponents.Where(c => c.Name == "TDS").Select(c => c.Id).FirstOrDefault()).Sum(l => (decimal?)l.Amount) ?? 0m }
            ).ToListAsync(ct);

            var tdsLineThisItem = await _db.PayrollRunItemLines
                .Where(l => l.PayrollRunItemId == item.Id && l.PayComponentId == _db.PayComponents.Where(c => c.Name == "TDS").Select(c => c.Id).FirstOrDefault())
                .SumAsync(l => (decimal?)l.Amount, ct) ?? 0m;

            var ytdGross = priorItemIds.Sum(p => p.GrossEarnings) + item.GrossEarnings;
            var ytdTax = priorItemIds.Sum(p => p.TdsLine) + tdsLineThisItem;

            _db.Payslips.Add(new Domain.Payroll.Payslip
            {
                TenantId = _tenant.TenantId, EmployeeId = item.EmployeeId, PayrollRunItemId = item.Id,
                PdfBlobUrl = $"pending-generation/{item.Id}", // PDF rendering is a follow-up integration, not built in this backend-only pass
                YtdGross = ytdGross, YtdTax = ytdTax
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    private static string ComputeFinancialYear(int year, int month)
        => month >= 4 ? $"{year}-{(year + 1) % 100:D2}" : $"{year - 1}-{year % 100:D2}";
}

public class GetPayslipsQueryHandler : IRequestHandler<GetPayslipsQuery, IReadOnlyList<PayslipDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly IPermissionChecker _permissionChecker;
    public GetPayslipsQueryHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver, IPermissionChecker permissionChecker)
    { _db = db; _employeeResolver = employeeResolver; _permissionChecker = permissionChecker; }

    public async Task<IReadOnlyList<PayslipDto>> Handle(GetPayslipsQuery request, CancellationToken ct)
    {
        // payslip.read.own is granted to every Employee for their own payslips; payslip.read (HR/admin) is
        // needed to view anyone else's — without this check any employee could enumerate another's payslips.
        var callerEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var canViewOthers = _permissionChecker.HasPermission(Permissions.PayslipRead);
        if (!canViewOthers && request.EmployeeId is not null && request.EmployeeId != callerEmployeeId)
            throw new ForbiddenException("You can only view your own payslips.");

        var effectiveEmployeeId = canViewOthers ? request.EmployeeId : callerEmployeeId;
        var query = _db.Payslips.AsQueryable();
        if (effectiveEmployeeId is not null) query = query.Where(p => p.EmployeeId == effectiveEmployeeId);

        return await query.OrderByDescending(p => p.GeneratedAtUtc)
            .Select(p => new PayslipDto(p.Id, p.EmployeeId, p.PayrollRunItemId, p.PdfBlobUrl, p.GeneratedAtUtc, p.YtdGross, p.YtdTax))
            .ToListAsync(ct);
    }
}

// ===== Full & Final Settlement =====
/// <summary>Simplified settlement: the employee's current monthly gross salary plus any unused leave balance
/// encashed at an approximate per-day rate. Precise statutory FFS rules (notice pay, bonus proration, tax on
/// encashment) are a documented follow-up — not a Phase 1 requirement.</summary>
public class ComputeFullFinalSettlementCommandHandler : IRequestHandler<ComputeFullFinalSettlementCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant;
    public ComputeFullFinalSettlementCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<Guid> Handle(ComputeFullFinalSettlementCommand request, CancellationToken ct)
    {
        var alreadyComputed = await _db.FullFinalSettlements.AnyAsync(s => s.ExitWorkflowRequestId == request.ExitWorkflowRequestId, ct);
        if (alreadyComputed) throw new ConflictException("A settlement has already been computed for this exit request.");

        var assignment = await _db.EmployeeSalaryAssignments.Where(a => a.EmployeeId == request.EmployeeId && a.EffectiveTo == null)
            .OrderByDescending(a => a.EffectiveFrom).FirstOrDefaultAsync(ct);
        var monthlyGross = 0m;
        if (assignment is not null)
        {
            monthlyGross = await (
                from v in _db.EmployeeSalaryComponentValues
                join pc in _db.PayComponents on v.PayComponentId equals pc.Id
                where v.AssignmentId == assignment.Id && pc.ComponentType == PayComponentType.Earning
                select v.ComputedAmount
            ).SumAsync(ct);
        }

        var currentYear = DateTime.UtcNow.Year;
        var unusedLeaveDays = await _db.LeaveBalances.Where(b => b.EmployeeId == request.EmployeeId && b.Year == currentYear)
            .SumAsync(b => (decimal?)(b.Accrued + b.CarriedForward - b.Used - b.Reserved), ct) ?? 0m;
        var perDayRate = monthlyGross / 30m;
        var leaveEncashment = unusedLeaveDays > 0 ? Math.Round(unusedLeaveDays * perDayRate, 2) : 0m;

        var settlement = new FullFinalSettlement
        {
            TenantId = _tenant.TenantId, EmployeeId = request.EmployeeId, ExitWorkflowRequestId = request.ExitWorkflowRequestId,
            NetSettlementAmount = Math.Round(monthlyGross + leaveEncashment, 2)
        };
        _db.FullFinalSettlements.Add(settlement);
        await _db.SaveChangesAsync(ct);
        return settlement.Id;
    }
}

public class GetFullFinalSettlementQueryHandler : IRequestHandler<GetFullFinalSettlementQuery, FullFinalSettlementDto?>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly IPermissionChecker _permissionChecker;
    public GetFullFinalSettlementQueryHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver, IPermissionChecker permissionChecker)
    { _db = db; _employeeResolver = employeeResolver; _permissionChecker = permissionChecker; }

    public async Task<FullFinalSettlementDto?> Handle(GetFullFinalSettlementQuery request, CancellationToken ct)
    {
        var callerEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        if (request.EmployeeId != callerEmployeeId && !_permissionChecker.HasPermission(Permissions.PayrollRunWrite))
            throw new ForbiddenException("You can only view your own settlement.");

        var settlement = await _db.FullFinalSettlements.Where(s => s.EmployeeId == request.EmployeeId).OrderByDescending(s => s.ComputedAtUtc).FirstOrDefaultAsync(ct);
        return settlement is null ? null : new FullFinalSettlementDto(settlement.Id, settlement.EmployeeId, settlement.ExitWorkflowRequestId, settlement.ComputedAtUtc, settlement.NetSettlementAmount, settlement.PayslipId);
    }
}
