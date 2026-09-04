using Microsoft.AspNetCore.Http;
using PeopleHQ.Application.Common.Interfaces;

namespace PeopleHQ.Infrastructure.Common;

public class PermissionChecker : IPermissionChecker
{
    private readonly IHttpContextAccessor _accessor;
    public PermissionChecker(IHttpContextAccessor accessor) => _accessor = accessor;

    public bool HasPermission(string permissionKey) => _accessor.HttpContext?.User?.HasClaim("permission", permissionKey) ?? false;
}
