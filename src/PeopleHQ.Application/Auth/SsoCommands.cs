using MediatR;

namespace PeopleHQ.Application.Auth;

// SSO (05-enhancements-and-roadmap.md Phase 4: "SSO (SAML/OIDC)"). v1 is OIDC-only — SAML is a documented
// follow-up. One SsoConfiguration row per tenant, configured by a TenantAdmin.

public record UpsertSsoConfigurationCommand(string ClientId, string ClientSecret, string Authority, bool IsEnabled) : IRequest;
public record GetSsoConfigurationQuery : IRequest<SsoConfigurationDto?>;
/// <summary>ClientSecret is write-only — never returned once stored.</summary>
public record SsoConfigurationDto(string ClientId, string Authority, bool IsEnabled);

/// <summary>Returns the provider's authorization URL to redirect the browser to.</summary>
public record InitiateSsoLoginCommand : IRequest<string>;
public record CompleteSsoLoginCommand(string Code, string State) : IRequest<AuthResult>;
