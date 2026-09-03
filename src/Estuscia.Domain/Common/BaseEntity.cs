namespace Estuscia.Domain.Common;

public interface IMultiTenantEntity
{
    int TenantId { get; set; }
}
public interface IBranchScopedEntity : IMultiTenantEntity
{
    int BranchId { get; set; }
}

public abstract class BaseEntity
{
    public int Id { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public int? CreatedByUserId { get; set; }
}