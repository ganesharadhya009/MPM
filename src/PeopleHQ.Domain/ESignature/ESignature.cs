using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.ESignature;

/// <summary>
/// Document e-signature ("most needed options" #1, 01-modules-functional-spec.md): "own simple
/// click-to-sign flow in v1; DocuSign/Adobe Sign integration later." v1 scope is employee-targeted requests
/// only (offer letters, contracts, policy acknowledgments already on file for an existing employee) with
/// self-service signing — a candidate-facing signing link would need an unauthenticated/token-based access
/// model distinct from the rest of this API's JWT auth, and is a documented follow-up, not built here.
/// </summary>
public enum ESignatureStatus { Pending, Signed, Declined }

public class ESignatureRequest : TenantOwnedEntity
{
    public string DocumentTitle { get; set; } = string.Empty;
    public string DocumentBlobUrl { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public Guid RequestedByEmployeeId { get; set; }
    public ESignatureStatus Status { get; set; } = ESignatureStatus.Pending;
    /// <summary>Typed full name at signing time — the "click-to-sign" consent artifact for v1 (no drawn
    /// signature capture / PDF stamping).</summary>
    public string? SignedByName { get; set; }
    public DateTime? SignedAtUtc { get; set; }
    public string? SignerIpAddress { get; set; }
}
