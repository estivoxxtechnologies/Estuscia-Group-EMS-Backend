using Estuscia.Domain.Common;
using Estuscia.Domain.Enums;

namespace Estuscia.Domain.Entities;

public class RoleModulePermission : BaseEntity, IMultiTenantEntity
{
    public Guid TenantId { get; set; }

    public UserRole Role { get; set; }

    public Guid ModuleId { get; set; }
    public Module Module { get; set; } = null!;

    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}