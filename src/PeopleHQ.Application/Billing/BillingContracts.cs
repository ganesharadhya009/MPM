using MediatR;
using PeopleHQ.Domain.Billing;

namespace PeopleHQ.Application.Billing;

// Self-serve billing / plan upgrade / seat-usage metering (05-enhancements-and-roadmap.md Phase 4).
// Plans are a global platform catalog (not tenant-owned); Invoices are per-tenant billing history.

public record GetSeatUsageQuery : IRequest<SeatUsageDto>;
public record SeatUsageDto(int SeatLimit, int ActiveEmployeeCount, int AvailableSeats, bool IsOverLimit);

public record GetPlansQuery : IRequest<IReadOnlyList<PlanDto>>;
public record PlanDto(Guid Id, string Name, int SeatLimit, decimal Price, string FeaturesJson);

public record UpgradePlanCommand(Guid NewPlanId) : IRequest<UpgradePlanResult>;
public record UpgradePlanResult(bool Succeeded, string? Error, Guid? InvoiceId);

public record GetInvoicesQuery : IRequest<IReadOnlyList<InvoiceDto>>;
public record InvoiceDto(Guid Id, Guid PlanId, decimal Amount, string Currency, InvoiceStatus Status, DateTime IssuedAtUtc, DateTime? PaidAtUtc);
