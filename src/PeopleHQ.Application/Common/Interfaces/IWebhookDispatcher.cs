using PeopleHQ.Domain.Integrations;

namespace PeopleHQ.Application.Common.Interfaces;

/// <summary>Dispatches an outbound webhook to every active tenant subscription matching eventType. Used by
/// module-specific notification handlers (starting with WorkflowRequestResolvedNotification) so they don't
/// need to know about HTTP delivery or signing.</summary>
public interface IWebhookDispatcher
{
    Task DispatchAsync(Guid tenantId, WebhookEventType eventType, object payload, CancellationToken ct = default);
}
