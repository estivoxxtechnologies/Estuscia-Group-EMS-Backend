using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Entities;
using Estuscia.Domain.Enums;

using Microsoft.EntityFrameworkCore;

namespace Estuscia.Infrastructure.Services;

public class PermissionService : IPermissionService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentTenantService _currentTenant;

    public PermissionService(
        IAppDbContext db,
        ICurrentTenantService currentTenant)
    {
        _db = db;
        _currentTenant = currentTenant;
    }

    public async Task<bool> HasPermissionAsync(
        string moduleCode,
        PermissionAction action,
        CancellationToken cancellationToken = default)
    {
        // ========================================================
        // AUTHENTICATION CHECK
        // ========================================================

        if (!_currentTenant.IsAuthenticated)
            return false;

        // ========================================================
        // SUPER ADMIN
        // ========================================================
        //
        // SuperAdmin has unrestricted permission access.
        // Tenant isolation is still handled separately by the
        // data-access layer / explicit SuperAdmin operations.
        // ========================================================

        if (_currentTenant.IsSuperAdmin)
            return true;

        // ========================================================
        // REQUIRED USER / TENANT CONTEXT
        // ========================================================

        if (!_currentTenant.UserId.HasValue ||
            !_currentTenant.TenantId.HasValue)
        {
            return false;
        }

        var userId =
            _currentTenant.UserId.Value;

        var tenantId =
            _currentTenant.TenantId.Value;

        // ========================================================
        // LOAD USER
        // ========================================================

        var user =
            await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == userId &&
                        x.TenantId == tenantId,
                    cancellationToken);

        if (user == null)
            return false;

        // ========================================================
        // USER-SPECIFIC OVERRIDE
        // ========================================================
        //
        // Nullable permission:
        //
        // true  -> explicitly allow
        // false -> explicitly deny
        // null  -> use role default
        // ========================================================

        var userPermission =
            await _db.UserModulePermissions
                .AsNoTracking()
                .Include(x => x.Module)
                .FirstOrDefaultAsync(
                    x =>
                        x.UserId == userId &&
                        x.TenantId == tenantId &&
                        x.Module.Code == moduleCode,
                    cancellationToken);

        if (userPermission != null)
        {
            var overrideResult =
                GetNullablePermission(
                    userPermission,
                    action);

            if (overrideResult.HasValue)
                return overrideResult.Value;
        }

        // ========================================================
        // ROLE DEFAULT
        // ========================================================

        var rolePermission =
            await _db.RoleModulePermissions
                .AsNoTracking()
                .Include(x => x.Module)
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.Role == user.Role &&
                        x.Module.Code == moduleCode,
                    cancellationToken);

        if (rolePermission == null)
            return false;

        return GetPermission(
            rolePermission,
            action);
    }

    // ============================================================
    // ROLE DEFAULT PERMISSION
    // ============================================================

    private static bool GetPermission(
        RoleModulePermission permission,
        PermissionAction action)
    {
        return action switch
        {
            PermissionAction.View =>
                permission.CanView,

            PermissionAction.Create =>
                permission.CanCreate,

            PermissionAction.Edit =>
                permission.CanEdit,

            PermissionAction.Delete =>
                permission.CanDelete,

            _ => false
        };
    }

    // ============================================================
    // USER OVERRIDE PERMISSION
    // ============================================================

    private static bool? GetNullablePermission(
        UserModulePermission permission,
        PermissionAction action)
    {
        return action switch
        {
            PermissionAction.View =>
                permission.CanView,

            PermissionAction.Create =>
                permission.CanCreate,

            PermissionAction.Edit =>
                permission.CanEdit,

            PermissionAction.Delete =>
                permission.CanDelete,

            _ => null
        };
    }
}
