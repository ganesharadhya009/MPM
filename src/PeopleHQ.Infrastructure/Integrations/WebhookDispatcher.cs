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
/// Delivers a webhook to every active tenant subscription matching eventType. Signs
/// "{unixTimestamp}.{payloadJson}" with HMAC-SHA256 using the subscription's own secret — binding the
/// timestamp into the signed material (not just alongside it) means an attacker can't swap in a fresh
/// timestamp on a captured request without invalidating the signature. Sent as X-PeopleHQ-Timestamp +
/// X-PeopleHQ-Signature, the same pattern Stripe/GitHub webhooks use; receivers should reject deliveries
/// whose timestamp is more than a few minutes old to close the replay window. Every attempt is recorded as
/// a WebhookDelivery row (Delivered/Failed), single-attempt in this pass — retry-with-backoff for Failed
/// deliveries is a documented follow-up, not built here.
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
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                using var request = new HttpRequestMessage(HttpMethod.Post, subscription.TargetUrl)
                {
                    Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("X-PeopleHQ-Timestamp", timestamp);
                request.Headers.Add("X-PeopleHQ-Signature", Sign(timestamp, payloadJson, subscription.Secret));
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

    private static string Sign(string timestamp, string payloadJson, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var signedBytes = Encoding.UTF8.GetBytes($"{timestamp}.{payloadJson}");
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(signedBytes));
    }
}
