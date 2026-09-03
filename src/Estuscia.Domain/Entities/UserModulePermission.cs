using Estuscia.Domain.Common;

namespace Estuscia.Domain.Entities;

public class UserModulePermission : BaseEntity, IMultiTenantEntity
{
    public int TenantId { get; set; }

    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public int ModuleId { get; set; }
    public Module Module { get; set; } = null!;

    public bool? CanView { get; set; }
    public bool? CanCreate { get; set; }
    public bool? CanEdit { get; set; }
    public bool? CanDelete { get; set; }
}