using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace PeopleHQ.Api.Authorization;

/// <summary>Every controller action decorated with [RequirePermission] references a Permissions.* constant, never a raw string.</summary>
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permissionKey) : base(policy: permissionKey) { }
}

public class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionKey { get; }
    public PermissionRequirement(string permissionKey) => PermissionKey = permissionKey;
}

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim("permission", requirement.PermissionKey))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

/// <summary>Resolves any [RequirePermission("x.y")] policy name to a PermissionRequirement dynamically — no per-permission pre-registration needed.</summary>
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        => _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();
        return Task.FromResult(policy);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
}
