using Microsoft.Extensions.Logging;
using PeopleHQ.Application.Common.Interfaces;

namespace PeopleHQ.Infrastructure.Billing;

/// <summary>
/// Placeholder IPaymentGateway: always succeeds and logs the charge instead of dispatching through a real
/// processor. Wiring an actual provider (Stripe/Razorpay/etc.) is a documented follow-up — out of scope for
/// this backend pass, same v1-simplification treatment as LoggingEmailSender for outbound email.
/// </summary>
public class MockPaymentGateway : IPaymentGateway
{
    private readonly ILogger<MockPaymentGateway> _logger;
    public MockPaymentGateway(ILogger<MockPaymentGateway> logger) => _logger = logger;

    public Task<PaymentChargeResult> ChargeAsync(Guid tenantId, decimal amount, string currency, string description, CancellationToken ct = default)
    {
        var reference = $"mock_{Guid.NewGuid():N}";
        _logger.LogInformation("Payment (placeholder gateway) for tenant {TenantId}: {Amount} {Currency} — {Description} — ref {Reference}",
            tenantId, amount, currency, description, reference);
        return Task.FromResult(new PaymentChargeResult(true, reference, null));
    }
}
