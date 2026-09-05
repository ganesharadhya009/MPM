using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.Identity;

/// <summary>
/// Phase 4 SSO (05-enhancements-and-roadmap.md: "SSO (SAML/OIDC)"). v1 implements OIDC only — SAML would
/// need a dedicated library (e.g. Sustainsys.Saml2) and is a documented follow-up, not built here. One row
/// per tenant (single-row-per-tenant pattern, matching StatutorySettings).
/// </summary>
public class SsoConfiguration : TenantOwnedEntity
{
    public string ClientId { get; set; } = string.Empty;
    /// <summary>TODO(Phase 4 hardening): encrypt at rest via Key Vault-backed DataProtection — same
    /// documented gap as AppUser.MfaSecretEncrypted, not addressed here either.</summary>
    public string ClientSecret { get; set; } = string.Empty;
    /// <summary>The OIDC issuer base URL — "{Authority}/.well-known/openid-configuration" is fetched for
    /// endpoint discovery.</summary>
    public string Authority { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}
