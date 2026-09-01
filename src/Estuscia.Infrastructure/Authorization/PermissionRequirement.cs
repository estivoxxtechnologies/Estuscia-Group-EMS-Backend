using Estuscia.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Estuscia.Infrastructure.Authorization;

public sealed class PermissionRequirement
    : IAuthorizationRequirement
{
    public string ModuleCode { get; }

    public PermissionAction Action { get; }

    public PermissionRequirement(
        string moduleCode,
        PermissionAction action)
    {
        ModuleCode = moduleCode;
        Action = action;
    }
}