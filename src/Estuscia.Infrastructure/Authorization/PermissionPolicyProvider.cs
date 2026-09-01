using Estuscia.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Estuscia.Infrastructure.Authorization;

public sealed class PermissionPolicyProvider
    : DefaultAuthorizationPolicyProvider
{
    public const string Prefix = "Permission:";

    public PermissionPolicyProvider(
        IOptions<AuthorizationOptions> options)
        : base(options)
    {
    }

    public override async Task<AuthorizationPolicy?>
        GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(
                Prefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return await base.GetPolicyAsync(policyName);
        }

        var value =
            policyName[Prefix.Length..];

        var parts =
            value.Split(
                ':',
                StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2)
            return null;

        var moduleCode = parts[0];

        if (!Enum.TryParse<PermissionAction>(
                parts[1],
                true,
                out var action))
        {
            return null;
        }

        var policy =
            new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(
                    new PermissionRequirement(
                        moduleCode,
                        action))
                .Build();

        return policy;
    }
}
