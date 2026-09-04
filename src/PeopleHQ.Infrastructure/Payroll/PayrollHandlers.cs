using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.Payroll;
using PeopleHQ.Domain.Auditing;
using PeopleHQ.Domain.Payroll;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Payroll;

// ===== Pay Components =====
public class CreatePayComponentCommandHandler : IRequestHandler<CreatePayComponentCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant; private readonly IAuditLogWriter _audit;
    public CreatePayComponentCommandHandler(AppDbContext db, ITenantContext tenant, IAuditLogWriter audit) { _db = db; _tenant = tenant; _audit = audit; }

    public async Task<Guid> Handle(CreatePayComponentCommand request, CancellationToken ct)
    {
        var component = new PayComponent
        {
            TenantId = _tenant.TenantId, Name = request.Name, ComponentType = request.ComponentType, AmountType = request.AmountType,
            FormulaJson = request.FormulaJson, IsTaxable = request.IsTaxable, IsStatutory = request.IsStatutory, SortOrder = request.SortOrder
        };
        _db.PayComponents.Add(component);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(PayComponent), component.Id, AuditAction.Create, null, component, ct);
        return component.Id;
    }
}

public class UpdatePayComponentCommandHandler : IRequestHandler<UpdatePayComponentCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public UpdatePayComponentCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(UpdatePayComponentCommand request, CancellationToken ct)
    {
        var component = await _db.PayComponents.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(PayComponent), request.Id);
        var before = new { component.Name, component.AmountType, component.FormulaJson, component.IsTaxable, component.SortOrder };
        component.Name = request.Name; component.AmountType = request.AmountType; component.FormulaJson = request.FormulaJson;
        component.IsTaxable = request.IsTaxable; component.SortOrder = request.SortOrder;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(PayComponent), component.Id, AuditAction.Update, before, component, ct);
    }
}

public class DeletePayComponentCommandHandler : IRequestHandler<DeletePayComponentCommand>
{
    private readonly AppDbContext _db; private readonly IAuditLogWriter _audit;
    public DeletePayComponentCommandHandler(AppDbContext db, IAuditLogWriter audit) { _db = db; _audit = audit; }

    public async Task Handle(DeletePayComponentCommand request, CancellationToken ct)
    {
        var component = await _db.PayComponents.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(PayComponent), request.Id);
        var inUse = await _db.SalaryStructureComponents.AnyAsync(c => c.PayComponentId == request.Id, ct);
        if (inUse) throw new ConflictException($"Pay component '{component.Name}' is used in a salary structure and cannot be deleted.");

        component.IsDeleted = true; component.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(PayComponent), component.Id, AuditAction.Delete, component, null, ct);
    }
}

public class GetPayComponentsQueryHandler : IRequestHandler<GetPayComponentsQuery, IReadOnlyList<PayComponentDto>>
{
    private readonly AppDbContext _db;
    public GetPayComponentsQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PayComponentDto>> Handle(GetPayComponentsQuery request, CancellationToken ct)
        => await _db.PayComponents.OrderBy(c => c.SortOrder)
            .Select(c => new PayComponentDto(c.Id, c.Name, c.ComponentType, c.AmountType, c.FormulaJson, c.IsTaxable, c.IsStatutory, c.SortOrder))
            .ToListAsync(ct);
}

// ===== Salary Structures =====
public class CreateSalaryStructureCommandHandler : IRequestHandler<CreateSalaryStructureCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant;
    public CreateSalaryStructureCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<Guid> Handle(CreateSalaryStructureCommand request, CancellationToken ct)
    {
        var structure = new SalaryStructure { TenantId = _tenant.TenantId, Name = request.Name, Description = request.Description };
        _db.SalaryStructures.Add(structure);
        await _db.SaveChangesAsync(ct);

        foreach (var component in request.Components)
            _db.SalaryStructureComponents.Add(new SalaryStructureComponent { SalaryStructureId = structure.Id, PayComponentId = component.PayComponentId, DefaultValue = component.DefaultValue, SortOrder = component.SortOrder });
        await _db.SaveChangesAsync(ct);
        return structure.Id;
    }
}

public class UpdateSalaryStructureCommandHandler : IRequestHandler<UpdateSalaryStructureCommand>
{
    private readonly AppDbContext _db;
    public UpdateSalaryStructureCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(UpdateSalaryStructureCommand request, CancellationToken ct)
    {
        var structure = await _db.SalaryStructures.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(SalaryStructure), request.Id);
        structure.Name = request.Name; structure.Description = request.Description;

        var existing = await _db.SalaryStructureComponents.Where(c => c.SalaryStructureId == structure.Id).ToListAsync(ct);
        _db.SalaryStructureComponents.RemoveRange(existing);
        foreach (var component in request.Components)
            _db.SalaryStructureComponents.Add(new SalaryStructureComponent { SalaryStructureId = structure.Id, PayComponentId = component.PayComponentId, DefaultValue = component.DefaultValue, SortOrder = component.SortOrder });

        await _db.SaveChangesAsync(ct);
    }
}

public class DeleteSalaryStructureCommandHandler : IRequestHandler<DeleteSalaryStructureCommand>
{
    private readonly AppDbContext _db;
    public DeleteSalaryStructureCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(DeleteSalaryStructureCommand request, CancellationToken ct)
    {
        var structure = await _db.SalaryStructures.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(SalaryStructure), request.Id);
        var inUse = await _db.EmployeeSalaryAssignments.AnyAsync(a => a.SalaryStructureId == request.Id, ct);
        if (inUse) throw new ConflictException($"Salary structure '{structure.Name}' has employee assignments and cannot be deleted.");

        var components = await _db.SalaryStructureComponents.Where(c => c.SalaryStructureId == structure.Id).ToListAsync(ct);
        _db.SalaryStructureComponents.RemoveRange(components);
        structure.IsDeleted = true; structure.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetSalaryStructuresQueryHandler : IRequestHandler<GetSalaryStructuresQuery, IReadOnlyList<SalaryStructureDto>>
{
    private readonly AppDbContext _db;
    public GetSalaryStructuresQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<SalaryStructureDto>> Handle(GetSalaryStructuresQuery request, CancellationToken ct)
    {
        var structures = await _db.SalaryStructures.OrderBy(s => s.Name).ToListAsync(ct);
        var structureIds = structures.Select(s => s.Id).ToList();
        var componentsByStructure = (await _db.SalaryStructureComponents.Where(c => structureIds.Contains(c.SalaryStructureId)).ToListAsync(ct)).ToLookup(c => c.SalaryStructureId);

        return structures.Select(s => new SalaryStructureDto(s.Id, s.Name, s.Description,
            componentsByStructure[s.Id].OrderBy(c => c.SortOrder).Select(c => new SalaryStructureComponentDto(c.PayComponentId, c.DefaultValue, c.SortOrder)).ToList()
        )).ToList();
    }
}

// ===== Employee Salary Assignment =====
public class AssignSalaryCommandHandler : IRequestHandler<AssignSalaryCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant; private readonly IAuditLogWriter _audit;
    public AssignSalaryCommandHandler(AppDbContext db, ITenantContext tenant, IAuditLogWriter audit) { _db = db; _tenant = tenant; _audit = audit; }

    public async Task<Guid> Handle(AssignSalaryCommand request, CancellationToken ct)
    {
        var structureComponents = await (
            from sc in _db.SalaryStructureComponents
            join pc in _db.PayComponents on sc.PayComponentId equals pc.Id
            where sc.SalaryStructureId == request.SalaryStructureId
            orderby sc.SortOrder
            select new { sc.PayComponentId, sc.DefaultValue, pc.AmountType, pc.Name }
        ).ToListAsync(ct);
        if (structureComponents.Count == 0) throw new NotFoundException(nameof(SalaryStructure), request.SalaryStructureId);

        // Resolve each component's monthly amount by AmountType. PercentOfBasic looks up the already-resolved
        // "Basic" component by name (sort order matters: Basic must be defined before anything computed off it).
        // Formula is a documented v1 simplification — DefaultValue is used verbatim, no expression engine yet.
        var monthlyCtc = request.CtcAnnual / 12m;
        var computed = new Dictionary<Guid, decimal>();
        decimal? basicValue = null;
        foreach (var component in structureComponents)
        {
            var amount = component.AmountType switch
            {
                PayComponentAmountType.Flat => component.DefaultValue,
                PayComponentAmountType.PercentOfCTC => Math.Round(monthlyCtc * component.DefaultValue / 100m, 2),
                PayComponentAmountType.PercentOfBasic => Math.Round((basicValue ?? 0m) * component.DefaultValue / 100m, 2),
                _ => component.DefaultValue
            };
            computed[component.PayComponentId] = amount;
            if (string.Equals(component.Name, "Basic", StringComparison.OrdinalIgnoreCase)) basicValue = amount;
        }

        var assignment = new EmployeeSalaryAssignment
        {
            TenantId = _tenant.TenantId, EmployeeId = request.EmployeeId, SalaryStructureId = request.SalaryStructureId,
            PayType = request.PayType, CtcAnnual = request.CtcAnnual, Currency = request.Currency, EffectiveFrom = request.EffectiveFrom
        };
        _db.EmployeeSalaryAssignments.Add(assignment);
        await _db.SaveChangesAsync(ct); // need assignment.Id for the component-value snapshot

        foreach (var (payComponentId, amount) in computed)
            _db.EmployeeSalaryComponentValues.Add(new EmployeeSalaryComponentValue { AssignmentId = assignment.Id, PayComponentId = payComponentId, ComputedAmount = amount });

        // FR-PAY-03: never overwrite — close out whatever open-ended assignment preceded this one instead.
        var priorOpenAssignment = await _db.EmployeeSalaryAssignments
            .Where(a => a.EmployeeId == request.EmployeeId && a.Id != assignment.Id && a.EffectiveTo == null && a.EffectiveFrom < request.EffectiveFrom)
            .OrderByDescending(a => a.EffectiveFrom).FirstOrDefaultAsync(ct);
        if (priorOpenAssignment is not null) priorOpenAssignment.EffectiveTo = request.EffectiveFrom.AddDays(-1);

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(nameof(EmployeeSalaryAssignment), assignment.Id, AuditAction.Create, null, assignment, ct);
        return assignment.Id;
    }
}

public class GetEmployeeSalaryHistoryQueryHandler : IRequestHandler<GetEmployeeSalaryHistoryQuery, IReadOnlyList<SalaryAssignmentDto>>
{
    private readonly AppDbContext _db;
    public GetEmployeeSalaryHistoryQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<SalaryAssignmentDto>> Handle(GetEmployeeSalaryHistoryQuery request, CancellationToken ct)
    {
        var assignments = await _db.EmployeeSalaryAssignments.Where(a => a.EmployeeId == request.EmployeeId).OrderByDescending(a => a.EffectiveFrom).ToListAsync(ct);
        var assignmentIds = assignments.Select(a => a.Id).ToList();
        var valuesByAssignment = (await _db.EmployeeSalaryComponentValues.Where(v => assignmentIds.Contains(v.AssignmentId)).ToListAsync(ct)).ToLookup(v => v.AssignmentId);

        return assignments.Select(a => new SalaryAssignmentDto(a.Id, a.EmployeeId, a.SalaryStructureId, a.PayType, a.CtcAnnual, a.Currency, a.EffectiveFrom, a.EffectiveTo,
            valuesByAssignment[a.Id].Select(v => new SalaryComponentValueDto(v.PayComponentId, v.ComputedAmount)).ToList()
        )).ToList();
    }
}

// ===== Statutory Settings / PT Slabs =====
public class UpsertStatutorySettingsCommandHandler : IRequestHandler<UpsertStatutorySettingsCommand>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant;
    public UpsertStatutorySettingsCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task Handle(UpsertStatutorySettingsCommand request, CancellationToken ct)
    {
        var settings = await _db.StatutorySettings.FirstOrDefaultAsync(ct); // one row per tenant, enforced by the global query filter
        if (settings is null)
        {
            settings = new StatutorySettings { TenantId = _tenant.TenantId };
            _db.StatutorySettings.Add(settings);
        }
        settings.CountryCode = request.CountryCode;
        settings.ConfigJson = request.ConfigJson;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetStatutorySettingsQueryHandler : IRequestHandler<GetStatutorySettingsQuery, StatutorySettingsDto?>
{
    private readonly AppDbContext _db;
    public GetStatutorySettingsQueryHandler(AppDbContext db) => _db = db;

    public async Task<StatutorySettingsDto?> Handle(GetStatutorySettingsQuery request, CancellationToken ct)
    {
        var settings = await _db.StatutorySettings.FirstOrDefaultAsync(ct);
        return settings is null ? null : new StatutorySettingsDto(settings.Id, settings.CountryCode, settings.ConfigJson);
    }
}

public class CreatePtSlabCommandHandler : IRequestHandler<CreatePtSlabCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant;
    public CreatePtSlabCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<Guid> Handle(CreatePtSlabCommand request, CancellationToken ct)
    {
        var slab = new PtSlab { TenantId = _tenant.TenantId, State = request.State, MinIncome = request.MinIncome, MaxIncome = request.MaxIncome, TaxAmount = request.TaxAmount };
        _db.PtSlabs.Add(slab);
        await _db.SaveChangesAsync(ct);
        return slab.Id;
    }
}

public class DeletePtSlabCommandHandler : IRequestHandler<DeletePtSlabCommand>
{
    private readonly AppDbContext _db;
    public DeletePtSlabCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(DeletePtSlabCommand request, CancellationToken ct)
    {
        var slab = await _db.PtSlabs.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(PtSlab), request.Id);
        slab.IsDeleted = true; slab.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetPtSlabsQueryHandler : IRequestHandler<GetPtSlabsQuery, IReadOnlyList<PtSlabDto>>
{
    private readonly AppDbContext _db;
    public GetPtSlabsQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PtSlabDto>> Handle(GetPtSlabsQuery request, CancellationToken ct)
    {
        var query = _db.PtSlabs.AsQueryable();
        if (request.State is not null) query = query.Where(s => s.State == request.State);
        return await query.OrderBy(s => s.State).ThenBy(s => s.MinIncome)
            .Select(s => new PtSlabDto(s.Id, s.State, s.MinIncome, s.MaxIncome, s.TaxAmount)).ToListAsync(ct);
    }
}

// ===== Investment Declarations & Tax Regime =====
public class CreateInvestmentDeclarationCommandHandler : IRequestHandler<CreateInvestmentDeclarationCommand, Guid>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant; private readonly ICurrentEmployeeResolver _employeeResolver;
    public CreateInvestmentDeclarationCommandHandler(AppDbContext db, ITenantContext tenant, ICurrentEmployeeResolver employeeResolver) { _db = db; _tenant = tenant; _employeeResolver = employeeResolver; }

    public async Task<Guid> Handle(CreateInvestmentDeclarationCommand request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var declaration = new InvestmentDeclaration
        {
            TenantId = _tenant.TenantId, EmployeeId = employeeId, FinancialYear = request.FinancialYear, Section = request.Section,
            DeclaredAmount = request.DeclaredAmount, ProofBlobUrl = request.ProofBlobUrl, Status = DeclarationStatus.Declared
        };
        _db.InvestmentDeclarations.Add(declaration);
        await _db.SaveChangesAsync(ct);
        return declaration.Id;
    }
}

public class VerifyInvestmentDeclarationCommandHandler : IRequestHandler<VerifyInvestmentDeclarationCommand>
{
    private readonly AppDbContext _db; private readonly ICurrentUserService _currentUser;
    public VerifyInvestmentDeclarationCommandHandler(AppDbContext db, ICurrentUserService currentUser) { _db = db; _currentUser = currentUser; }

    public async Task Handle(VerifyInvestmentDeclarationCommand request, CancellationToken ct)
    {
        var declaration = await _db.InvestmentDeclarations.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(InvestmentDeclaration), request.Id);
        declaration.Status = request.Status;
        declaration.VerifiedBy = _currentUser.UserId;
        declaration.VerifiedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetInvestmentDeclarationsQueryHandler : IRequestHandler<GetInvestmentDeclarationsQuery, IReadOnlyList<InvestmentDeclarationDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly IPermissionChecker _permissionChecker;
    public GetInvestmentDeclarationsQueryHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver, IPermissionChecker permissionChecker)
    { _db = db; _employeeResolver = employeeResolver; _permissionChecker = permissionChecker; }

    public async Task<IReadOnlyList<InvestmentDeclarationDto>> Handle(GetInvestmentDeclarationsQuery request, CancellationToken ct)
    {
        // investment declarations are financial PII — a caller without verify rights may only ever see their own.
        var callerEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var canViewOthers = _permissionChecker.HasPermission(Domain.Identity.Permissions.InvestmentDeclarationVerify);
        if (!canViewOthers && request.EmployeeId is not null && request.EmployeeId != callerEmployeeId)
            throw new ForbiddenException("You can only view your own investment declarations.");

        var effectiveEmployeeId = canViewOthers ? request.EmployeeId : callerEmployeeId;
        var query = _db.InvestmentDeclarations.AsQueryable();
        if (effectiveEmployeeId is not null) query = query.Where(d => d.EmployeeId == effectiveEmployeeId);
        if (request.FinancialYear is not null) query = query.Where(d => d.FinancialYear == request.FinancialYear);

        return await query.OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new InvestmentDeclarationDto(d.Id, d.EmployeeId, d.FinancialYear, d.Section, d.DeclaredAmount, d.ProofBlobUrl, d.Status))
            .ToListAsync(ct);
    }
}

public class SelectTaxRegimeCommandHandler : IRequestHandler<SelectTaxRegimeCommand>
{
    private readonly AppDbContext _db; private readonly ITenantContext _tenant; private readonly ICurrentEmployeeResolver _employeeResolver;
    public SelectTaxRegimeCommandHandler(AppDbContext db, ITenantContext tenant, ICurrentEmployeeResolver employeeResolver) { _db = db; _tenant = tenant; _employeeResolver = employeeResolver; }

    public async Task Handle(SelectTaxRegimeCommand request, CancellationToken ct)
    {
        var employeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var selection = await _db.EmployeeTaxRegimeSelections.FirstOrDefaultAsync(s => s.EmployeeId == employeeId && s.FinancialYear == request.FinancialYear, ct);
        if (selection is null)
        {
            selection = new EmployeeTaxRegimeSelection { TenantId = _tenant.TenantId, EmployeeId = employeeId, FinancialYear = request.FinancialYear };
            _db.EmployeeTaxRegimeSelections.Add(selection);
        }
        selection.Regime = request.Regime;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetTaxRegimeSelectionQueryHandler : IRequestHandler<GetTaxRegimeSelectionQuery, TaxRegimeSelectionDto?>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly IPermissionChecker _permissionChecker;
    public GetTaxRegimeSelectionQueryHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver, IPermissionChecker permissionChecker)
    { _db = db; _employeeResolver = employeeResolver; _permissionChecker = permissionChecker; }

    public async Task<TaxRegimeSelectionDto?> Handle(GetTaxRegimeSelectionQuery request, CancellationToken ct)
    {
        var callerEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        if (request.EmployeeId != callerEmployeeId && !_permissionChecker.HasPermission(Domain.Identity.Permissions.InvestmentDeclarationVerify))
            throw new ForbiddenException("You can only view your own tax regime selection.");

        var selection = await _db.EmployeeTaxRegimeSelections.FirstOrDefaultAsync(s => s.EmployeeId == request.EmployeeId && s.FinancialYear == request.FinancialYear, ct);
        return selection is null ? null : new TaxRegimeSelectionDto(selection.EmployeeId, selection.FinancialYear, selection.Regime);
    }
}
