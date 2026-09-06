using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.Billing;

/// <summary>
/// Phase 4 self-serve billing (05-enhancements-and-roadmap.md: "Self-serve billing/plan upgrade, seat-usage
/// metering"). One Invoice row per billing event (a plan upgrade charge). Actual payment processing goes
/// through IPaymentGateway — a placeholder implementation in v1 (no real Stripe/Razorpay account to
/// integrate against), same documented-follow-up treatment as LoggingEmailSender for outbound email.
/// </summary>
public enum InvoiceStatus { Pending, Paid, Failed }

public class Invoice : TenantOwnedEntity
{
    public Guid PlanId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;
    public string? PaymentReference { get; set; }
    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAtUtc { get; set; }
}
