using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.Integrations;

/// <summary>
/// Phase 4 (05-enhancements-and-roadmap.md): "API keys + webhooks for tenants ... mid-market customers
/// expect to be able to connect their own tools." Scope note: this pass covers key/subscription issuance,
/// management, and outbound webhook dispatch on business events. The inbound request-time authentication
/// handler that accepts an X-Api-Key header as an alternative to the JWT scheme is a documented follow-up —
/// not built in this pass.
/// </summary>
public enum WebhookEventType { WorkflowRequestResolved }
public enum WebhookDeliveryStatus { Delivered, Failed }

/// <summary>The plaintext key is shown to the caller exactly once at creation time and never stored —
/// only its SHA-256 hash (KeyHash) is persisted, checked against on each future request.</summary>
public class ApiKey : TenantOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    /// <summary>First few characters of the plaintext key, kept for UI identification (e.g. "phq_ab12...").</summary>
    public string KeyPrefix { get; set; } = string.Empty;
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}

/// <summary>Secret is an HMAC-SHA256 signing key, shown once at creation and stored in plaintext so
/// deliveries can be (re-)signed — analogous to how Stripe/GitHub webhook secrets work.</summary>
public class WebhookSubscription : TenantOwnedEntity
{
    public string TargetUrl { get; set; } = string.Empty;
    public WebhookEventType EventType { get; set; }
    public string Secret { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class WebhookDelivery : TenantOwnedEntity
{
    public Guid WebhookSubscriptionId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public WebhookDeliveryStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public int? ResponseStatusCode { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
}
