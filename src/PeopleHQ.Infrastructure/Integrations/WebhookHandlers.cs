using System.Security.Cryptography;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.Integrations;
using PeopleHQ.Domain.Integrations;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Integrations;

public class CreateWebhookSubscriptionCommandHandler : IRequestHandler<CreateWebhookSubscriptionCommand, CreateWebhookSubscriptionResult>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public CreateWebhookSubscriptionCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<CreateWebhookSubscriptionResult> Handle(CreateWebhookSubscriptionCommand request, CancellationToken ct)
    {
        if (!Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ValidationException(nameof(request.TargetUrl), "TargetUrl must be an absolute https:// URL.");

        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var subscription = new WebhookSubscription
        {
            TenantId = _tenant.TenantId,
            TargetUrl = request.TargetUrl,
            EventType = request.EventType,
            Secret = secret,
            IsActive = true
        };
        _db.WebhookSubscriptions.Add(subscription);
        await _db.SaveChangesAsync(ct);
        return new CreateWebhookSubscriptionResult(subscription.Id, secret);
    }
}

public class DeleteWebhookSubscriptionCommandHandler : IRequestHandler<DeleteWebhookSubscriptionCommand>
{
    private readonly AppDbContext _db;
    public DeleteWebhookSubscriptionCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(DeleteWebhookSubscriptionCommand request, CancellationToken ct)
    {
        var subscription = await _db.WebhookSubscriptions.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(WebhookSubscription), request.Id);
        subscription.IsActive = false;
        subscription.IsDeleted = true;
        subscription.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetWebhookSubscriptionsQueryHandler : IRequestHandler<GetWebhookSubscriptionsQuery, IReadOnlyList<WebhookSubscriptionDto>>
{
    private readonly AppDbContext _db;
    public GetWebhookSubscriptionsQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<WebhookSubscriptionDto>> Handle(GetWebhookSubscriptionsQuery request, CancellationToken ct)
        => await _db.WebhookSubscriptions
            .Select(s => new WebhookSubscriptionDto(s.Id, s.TargetUrl, s.EventType, s.IsActive))
            .ToListAsync(ct);
}

public class GetWebhookDeliveriesQueryHandler : IRequestHandler<GetWebhookDeliveriesQuery, IReadOnlyList<WebhookDeliveryDto>>
{
    private readonly AppDbContext _db;
    public GetWebhookDeliveriesQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<WebhookDeliveryDto>> Handle(GetWebhookDeliveriesQuery request, CancellationToken ct)
        => await _db.WebhookDeliveries
            .Where(d => d.WebhookSubscriptionId == request.WebhookSubscriptionId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new WebhookDeliveryDto(d.Id, d.Status, d.AttemptCount, d.ResponseStatusCode, d.DeliveredAtUtc, d.CreatedAtUtc))
            .ToListAsync(ct);
}
