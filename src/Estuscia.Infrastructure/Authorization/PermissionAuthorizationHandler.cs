using Estuscia.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace Estuscia.Infrastructure.Authorization;

public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;

    public PermissionAuthorizationHandler(
        IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return;

        var allowed =
            await _permissionService.HasPermissionAsync(
                requirement.ModuleCode,
                requirement.Action);

        if (allowed)
        {
            context.Succeed(requirement);
        }
    }
}
