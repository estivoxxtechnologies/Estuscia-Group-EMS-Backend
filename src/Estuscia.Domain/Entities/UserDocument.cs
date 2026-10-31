
using Estuscia.Domain.Common;

namespace Estuscia.Domain.Entities;

public class UserDocument : BaseEntity, IMultiTenantEntity
{
    public int UserId { get; set; }
    public int? TenantId { get; set; }
    public int? BranchId { get; set; }

    // Examples: EducationCertificate, Aadhaar, PAN,
    // BankProof, PassportPhoto.
    public string DocumentType { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    // Example: AzureBlob, S3.
    public string StorageProvider { get; set; } = string.Empty;

    // Private storage key, not a public URL.
    public string StorageKey { get; set; } = string.Empty;

    public string VerificationStatus { get; set; } = "Pending";

    public int? VerifiedByUserId { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public string? RejectionReason { get; set; }

    public int UploadedByUserId { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    public ApplicationUser? User { get; set; }
}
