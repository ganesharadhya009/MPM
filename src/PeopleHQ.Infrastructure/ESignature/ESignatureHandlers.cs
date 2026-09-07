using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.ESignature;
using PeopleHQ.Domain.ESignature;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.ESignature;

public class CreateESignatureRequestCommandHandler : IRequestHandler<CreateESignatureRequestCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public CreateESignatureRequestCommandHandler(AppDbContext db, ITenantContext tenant, ICurrentEmployeeResolver employeeResolver)
    { _db = db; _tenant = tenant; _employeeResolver = employeeResolver; }

    public async Task<Guid> Handle(CreateESignatureRequestCommand request, CancellationToken ct)
    {
        var requestedBy = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var esignRequest = new ESignatureRequest
        {
            TenantId = _tenant.TenantId,
            DocumentTitle = request.DocumentTitle,
            DocumentBlobUrl = request.DocumentBlobUrl,
            EmployeeId = request.EmployeeId,
            RequestedByEmployeeId = requestedBy,
            Status = ESignatureStatus.Pending
        };
        _db.ESignatureRequests.Add(esignRequest);
        await _db.SaveChangesAsync(ct);
        return esignRequest.Id;
    }
}

public class SignDocumentCommandHandler : IRequestHandler<SignDocumentCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public SignDocumentCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task Handle(SignDocumentCommand request, CancellationToken ct)
    {
        var esignRequest = await _db.ESignatureRequests.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new NotFoundException(nameof(ESignatureRequest), request.Id);

        var callerEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        if (esignRequest.EmployeeId != callerEmployeeId)
            throw new ForbiddenException("You can only sign your own document requests.");
        if (esignRequest.Status != ESignatureStatus.Pending)
            throw new ConflictException("This document request has already been resolved.");

        esignRequest.Status = ESignatureStatus.Signed;
        esignRequest.SignedByName = request.SignedByName;
        esignRequest.SignedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class DeclineDocumentCommandHandler : IRequestHandler<DeclineDocumentCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    public DeclineDocumentCommandHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver) { _db = db; _employeeResolver = employeeResolver; }

    public async Task Handle(DeclineDocumentCommand request, CancellationToken ct)
    {
        var esignRequest = await _db.ESignatureRequests.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new NotFoundException(nameof(ESignatureRequest), request.Id);

        var callerEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        if (esignRequest.EmployeeId != callerEmployeeId)
            throw new ForbiddenException("You can only decline your own document requests.");
        if (esignRequest.Status != ESignatureStatus.Pending)
            throw new ConflictException("This document request has already been resolved.");

        esignRequest.Status = ESignatureStatus.Declined;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetESignatureRequestsQueryHandler : IRequestHandler<GetESignatureRequestsQuery, IReadOnlyList<ESignatureRequestDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentEmployeeResolver _employeeResolver;
    private readonly IPermissionChecker _permissionChecker;
    public GetESignatureRequestsQueryHandler(AppDbContext db, ICurrentEmployeeResolver employeeResolver, IPermissionChecker permissionChecker)
    { _db = db; _employeeResolver = employeeResolver; _permissionChecker = permissionChecker; }

    public async Task<IReadOnlyList<ESignatureRequestDto>> Handle(GetESignatureRequestsQuery request, CancellationToken ct)
    {
        var callerEmployeeId = await _employeeResolver.GetCurrentEmployeeIdAsync(ct);
        var canViewOthers = _permissionChecker.HasPermission(Domain.Identity.Permissions.ESignatureRequestWrite);
        if (!canViewOthers && request.EmployeeId is not null && request.EmployeeId != callerEmployeeId)
            throw new ForbiddenException("You can only view your own document requests.");

        var effectiveEmployeeId = canViewOthers ? request.EmployeeId : callerEmployeeId;

        var query = _db.ESignatureRequests.AsQueryable();
        if (effectiveEmployeeId is not null) query = query.Where(r => r.EmployeeId == effectiveEmployeeId);
        if (request.Status is not null) query = query.Where(r => r.Status == request.Status);

        return await query.OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new ESignatureRequestDto(r.Id, r.DocumentTitle, r.DocumentBlobUrl, r.EmployeeId, r.Status, r.SignedByName, r.SignedAtUtc))
            .ToListAsync(ct);
    }
}
