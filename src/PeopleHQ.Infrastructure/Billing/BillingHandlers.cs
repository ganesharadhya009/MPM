using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Billing;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Domain.Billing;
using PeopleHQ.Domain.Employees;
using PeopleHQ.Domain.Tenancy;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Billing;

public class GetSeatUsageQueryHandler : IRequestHandler<GetSeatUsageQuery, SeatUsageDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public GetSeatUsageQueryHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<SeatUsageDto> Handle(GetSeatUsageQuery request, CancellationToken ct)
    {
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == _tenant.TenantId, ct)
            ?? throw new NotFoundException(nameof(Tenant), _tenant.TenantId);
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == tenant.PlanId, ct)
            ?? throw new NotFoundException(nameof(Plan), tenant.PlanId);

        var activeCount = await _db.Employees.CountAsync(e => e.Status == EmployeeStatus.Active, ct);
        var available = plan.SeatLimit - activeCount;
        return new SeatUsageDto(plan.SeatLimit, activeCount, Math.Max(0, available), activeCount > plan.SeatLimit);
    }
}

public class GetPlansQueryHandler : IRequestHandler<GetPlansQuery, IReadOnlyList<PlanDto>>
{
    private readonly AppDbContext _db;
    public GetPlansQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PlanDto>> Handle(GetPlansQuery request, CancellationToken ct)
        => await _db.Plans
            .OrderBy(p => p.Price)
            .Select(p => new PlanDto(p.Id, p.Name, p.SeatLimit, p.Price, p.FeaturesJson))
            .ToListAsync(ct);
}

public class UpgradePlanCommandHandler : IRequestHandler<UpgradePlanCommand, UpgradePlanResult>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPaymentGateway _paymentGateway;
    public UpgradePlanCommandHandler(AppDbContext db, ITenantContext tenant, IPaymentGateway paymentGateway)
    { _db = db; _tenant = tenant; _paymentGateway = paymentGateway; }

    public async Task<UpgradePlanResult> Handle(UpgradePlanCommand request, CancellationToken ct)
    {
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == _tenant.TenantId, ct)
            ?? throw new NotFoundException(nameof(Tenant), _tenant.TenantId);
        var newPlan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == request.NewPlanId, ct)
            ?? throw new NotFoundException(nameof(Plan), request.NewPlanId);

        var activeCount = await _db.Employees.CountAsync(e => e.Status == EmployeeStatus.Active, ct);
        if (activeCount > newPlan.SeatLimit)
            return new UpgradePlanResult(false, $"This plan supports {newPlan.SeatLimit} seats, but {activeCount} employees are currently active.", null);

        var invoice = new Invoice
        {
            TenantId = _tenant.TenantId,
            PlanId = newPlan.Id,
            Amount = newPlan.Price,
            Currency = "INR",
            Status = InvoiceStatus.Pending
        };
        _db.Invoices.Add(invoice);

        var chargeResult = await _paymentGateway.ChargeAsync(_tenant.TenantId, newPlan.Price, "INR", $"Plan upgrade to {newPlan.Name}", ct);
        if (!chargeResult.Succeeded)
        {
            invoice.Status = InvoiceStatus.Failed;
            await _db.SaveChangesAsync(ct);
            return new UpgradePlanResult(false, chargeResult.Error ?? "Payment failed.", invoice.Id);
        }

        invoice.Status = InvoiceStatus.Paid;
        invoice.PaymentReference = chargeResult.PaymentReference;
        invoice.PaidAtUtc = DateTime.UtcNow;
        tenant.PlanId = newPlan.Id;

        await _db.SaveChangesAsync(ct);
        return new UpgradePlanResult(true, null, invoice.Id);
    }
}

public class GetInvoicesQueryHandler : IRequestHandler<GetInvoicesQuery, IReadOnlyList<InvoiceDto>>
{
    private readonly AppDbContext _db;
    public GetInvoicesQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<InvoiceDto>> Handle(GetInvoicesQuery request, CancellationToken ct)
        => await _db.Invoices
            .OrderByDescending(i => i.IssuedAtUtc)
            .Select(i => new InvoiceDto(i.Id, i.PlanId, i.Amount, i.Currency, i.Status, i.IssuedAtUtc, i.PaidAtUtc))
            .ToListAsync(ct);
}
