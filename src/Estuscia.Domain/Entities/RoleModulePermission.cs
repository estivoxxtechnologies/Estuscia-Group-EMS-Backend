using Estuscia.Domain.Common;
using Estuscia.Domain.Enums;

namespace Estuscia.Domain.Entities;

public class RoleModulePermission : BaseEntity, IMultiTenantEntity
{
    public int TenantId { get; set; }

    public int RoleNumber { get; set; }

    public Role Role { get; set; } = null!;

    public int ModuleId { get; set; }
    public Module Module { get; set; } = null!;

    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}