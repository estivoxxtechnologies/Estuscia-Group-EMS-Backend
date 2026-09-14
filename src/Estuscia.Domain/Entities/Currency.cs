using Estuscia.Domain.Common;

namespace Estuscia.Domain.Entities;

public class Currency : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Symbol { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<Tenant> Tenants { get; set; }
        = new List<Tenant>();

    public ICollection<TenantBranch> Branches { get; set; }
        = new List<TenantBranch>();
}