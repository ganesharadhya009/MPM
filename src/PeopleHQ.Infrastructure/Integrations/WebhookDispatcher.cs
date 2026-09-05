using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Domain.Integrations;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Integrations;

/// <summary>
/// Delivers a webhook to every active tenant subscription matching eventType. Signs the JSON body with
/// HMAC-SHA256 using the subscription's own secret, sent as the X-PeopleHQ-Signature header (hex-encoded),
/// so the receiver can verify authenticity — the same pattern Stripe/GitHub webhooks use. Every attempt is
/// recorded as a WebhookDelivery row (Delivered/Failed), single-attempt in this pass — retry-with-backoff
/// for Failed deliveries is a documented follow-up, not built here.
/// </summary>
public class WebhookDispatcher : IWebhookDispatcher
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDispatcher> _logger;

    public WebhookDispatcher(AppDbContext db, IHttpClientFactory httpClientFactory, ILogger<WebhookDispatcher> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task DispatchAsync(Guid tenantId, WebhookEventType eventType, object payload, CancellationToken ct = default)
    {
        var subscriptions = await _db.WebhookSubscriptions
            .Where(s => s.TenantId == tenantId && s.EventType == eventType && s.IsActive)
            .ToListAsync(ct);
        if (subscriptions.Count == 0) return;

        var payloadJson = JsonSerializer.Serialize(payload);
        var client = _httpClientFactory.CreateClient(nameof(WebhookDispatcher));

        foreach (var subscription in subscriptions)
        {
            var delivery = new Domain.Integrations.WebhookDelivery
            {
                TenantId = tenantId,
                WebhookSubscriptionId = subscription.Id,
                PayloadJson = payloadJson,
                AttemptCount = 1
            };

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, subscription.TargetUrl)
                {
                    Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("X-PeopleHQ-Signature", Sign(payloadJson, subscription.Secret));
                request.Headers.Add("X-PeopleHQ-Event", eventType.ToString());

                using var response = await client.SendAsync(request, ct);
                delivery.ResponseStatusCode = (int)response.StatusCode;
                delivery.Status = response.IsSuccessStatusCode ? WebhookDeliveryStatus.Delivered : WebhookDeliveryStatus.Failed;
                delivery.DeliveredAtUtc = response.IsSuccessStatusCode ? DateTime.UtcNow : null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Webhook delivery failed for subscription {SubscriptionId}", subscription.Id);
                delivery.Status = WebhookDeliveryStatus.Failed;
            }

            _db.WebhookDeliveries.Add(delivery);
        }

        await _db.SaveChangesAsync(ct);
    }

    private static string Sign(string payloadJson, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(payloadBytes));
    }
}
