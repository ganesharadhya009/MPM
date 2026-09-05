using MediatR;
using PeopleHQ.Domain.Integrations;

namespace PeopleHQ.Application.Integrations;

// API keys + webhooks for tenants (05-enhancements-and-roadmap.md Phase 4). Plaintext key/secret values
// are returned exactly once, at creation time, and never again — only their hash (ApiKey) or the value
// itself for signing (WebhookSubscription.Secret) is persisted.

public record CreateApiKeyCommand(string Name, DateTime? ExpiresAtUtc) : IRequest<CreateApiKeyResult>;
public record CreateApiKeyResult(Guid Id, string PlaintextKey);
public record RevokeApiKeyCommand(Guid Id) : IRequest;
public record GetApiKeysQuery : IRequest<IReadOnlyList<ApiKeyDto>>;
public record ApiKeyDto(Guid Id, string Name, string KeyPrefix, DateTime? LastUsedAtUtc, DateTime? RevokedAtUtc, DateTime? ExpiresAtUtc);

public record CreateWebhookSubscriptionCommand(string TargetUrl, WebhookEventType EventType) : IRequest<CreateWebhookSubscriptionResult>;
public record CreateWebhookSubscriptionResult(Guid Id, string Secret);
public record DeleteWebhookSubscriptionCommand(Guid Id) : IRequest;
public record GetWebhookSubscriptionsQuery : IRequest<IReadOnlyList<WebhookSubscriptionDto>>;
public record WebhookSubscriptionDto(Guid Id, string TargetUrl, WebhookEventType EventType, bool IsActive);

public record GetWebhookDeliveriesQuery(Guid WebhookSubscriptionId) : IRequest<IReadOnlyList<WebhookDeliveryDto>>;
public record WebhookDeliveryDto(Guid Id, WebhookDeliveryStatus Status, int AttemptCount, int? ResponseStatusCode, DateTime? DeliveredAtUtc, DateTime CreatedAtUtc);
