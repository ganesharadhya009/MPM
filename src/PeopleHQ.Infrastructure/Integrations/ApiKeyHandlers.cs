using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.Integrations;
using PeopleHQ.Domain.Integrations;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Integrations;

public class CreateApiKeyCommandHandler : IRequestHandler<CreateApiKeyCommand, CreateApiKeyResult>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public CreateApiKeyCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<CreateApiKeyResult> Handle(CreateApiKeyCommand request, CancellationToken ct)
    {
        var (plaintext, prefix) = ApiKeyHasher.GenerateKey();
        var apiKey = new ApiKey
        {
            TenantId = _tenant.TenantId,
            Name = request.Name,
            KeyHash = ApiKeyHasher.Hash(plaintext),
            KeyPrefix = prefix,
            ExpiresAtUtc = request.ExpiresAtUtc
        };
        _db.ApiKeys.Add(apiKey);
        await _db.SaveChangesAsync(ct);
        return new CreateApiKeyResult(apiKey.Id, plaintext);
    }
}

public class RevokeApiKeyCommandHandler : IRequestHandler<RevokeApiKeyCommand>
{
    private readonly AppDbContext _db;
    public RevokeApiKeyCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(RevokeApiKeyCommand request, CancellationToken ct)
    {
        var apiKey = await _db.ApiKeys.FindAsync(new object[] { request.Id }, ct) ?? throw new NotFoundException(nameof(ApiKey), request.Id);
        apiKey.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetApiKeysQueryHandler : IRequestHandler<GetApiKeysQuery, IReadOnlyList<ApiKeyDto>>
{
    private readonly AppDbContext _db;
    public GetApiKeysQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ApiKeyDto>> Handle(GetApiKeysQuery request, CancellationToken ct)
        => await _db.ApiKeys
            .Select(k => new ApiKeyDto(k.Id, k.Name, k.KeyPrefix, k.LastUsedAtUtc, k.RevokedAtUtc, k.ExpiresAtUtc))
            .ToListAsync(ct);
}
