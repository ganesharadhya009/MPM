namespace PeopleHQ.Application.Common.Interfaces;

/// <summary>Abstraction over the payment processor, so a real provider (Stripe, Razorpay, etc.) can be
/// swapped in Infrastructure without touching callers. See MockPaymentGateway for the placeholder v1
/// implementation — same documented-follow-up treatment as IEmailSender.</summary>
public interface IPaymentGateway
{
    Task<PaymentChargeResult> ChargeAsync(Guid tenantId, decimal amount, string currency, string description, CancellationToken ct = default);
}

public record PaymentChargeResult(bool Succeeded, string? PaymentReference, string? Error);
