using Estuscia.Domain.Common;

namespace Estuscia.Domain.Entities;

public class UserModulePermission : BaseEntity, IMultiTenantEntity
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public Guid ModuleId { get; set; }
    public Module Module { get; set; } = null!;

    public bool? CanView { get; set; }
    public bool? CanCreate { get; set; }
    public bool? CanEdit { get; set; }
    public bool? CanDelete { get; set; }
}