namespace Estuscia.Domain.Common;

public interface IMultiTenantEntity
{
    Guid TenantId { get; set; }
}

public interface IBranchScopedEntity : IMultiTenantEntity
{
    string BranchName { get; set; }
}

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public string? CreatedByUserId { get; set; }
}
