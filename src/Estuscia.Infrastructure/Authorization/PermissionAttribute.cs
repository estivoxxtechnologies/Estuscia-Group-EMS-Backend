using Estuscia.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Estuscia.Infrastructure.Authorization;

[AttributeUsage(
    AttributeTargets.Class |
    AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = true)]
public sealed class PermissionAttribute
    : AuthorizeAttribute
{
    public PermissionAttribute(
        string moduleCode,
        PermissionAction action)
    {
        Policy =
            $"{PermissionPolicyProvider.Prefix}" +
            $"{moduleCode}:{action}";
    }
}