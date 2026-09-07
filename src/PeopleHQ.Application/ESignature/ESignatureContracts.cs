using MediatR;
using PeopleHQ.Domain.ESignature;

namespace PeopleHQ.Application.ESignature;

// Document e-signature ("most needed options" #1). v1: employee-targeted requests, self-service signing —
// SignDocumentCommand/DeclineDocumentCommand always act on the CALLER's own pending request (resolved via
// ICurrentEmployeeResolver, verified against the request's EmployeeId), so there is no target-employee
// parameter to guard against IDOR.

public record CreateESignatureRequestCommand(string DocumentTitle, string DocumentBlobUrl, Guid EmployeeId) : IRequest<Guid>;
public record SignDocumentCommand(Guid Id, string SignedByName) : IRequest;
public record DeclineDocumentCommand(Guid Id) : IRequest;
public record GetESignatureRequestsQuery(Guid? EmployeeId = null, ESignatureStatus? Status = null) : IRequest<IReadOnlyList<ESignatureRequestDto>>;
public record ESignatureRequestDto(Guid Id, string DocumentTitle, string DocumentBlobUrl, Guid EmployeeId,
    ESignatureStatus Status, string? SignedByName, DateTime? SignedAtUtc);
