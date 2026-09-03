using Estuscia.Domain.Common;
using Estuscia.Domain.Enums;

namespace Estuscia.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Plan { get; set; } = "Enterprise Pro";
    public string Currency { get; set; } = "USD ($)";
    public bool IsActive { get; set; } = true;
    public List<TenantBranch> Branches { get; set; } = new();
    public List<ApplicationUser> Users { get; set; } = new();
}

public class Role : BaseEntity
{
    public int RoleNumber { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<ApplicationUser> Users { get; set; }
        = new List<ApplicationUser>();
}

public class TenantBranch : BaseEntity, IMultiTenantEntity
{
    public int TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public string BranchName { get; set; } = string.Empty;

    public string? City { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ApplicationUser> Users { get; set; }
        = new List<ApplicationUser>();

    public ICollection<DailyWorkLog> DailyWorkLogs { get; set; }
        = new List<DailyWorkLog>();

    public ICollection<CustomerReceipt> CustomerReceipts { get; set; }
        = new List<CustomerReceipt>();

    public ICollection<AttendanceRecord> AttendanceRecords { get; set; }
        = new List<AttendanceRecord>();
}

public class ApplicationUser : BaseEntity, IMultiTenantEntity
{
    public int TenantId { get; set; }

    public int? BranchId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public TenantBranch? Branch { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string EmployeeCode { get; set; } = string.Empty;

    public int RoleNumber { get; set; }

    public Role Role { get; set; } = null!;

    public string Designation { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public decimal SalaryBase { get; set; }

    public string AvatarUrl { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public class DailyWorkLog : BaseEntity, IBranchScopedEntity
{
    public int TenantId { get; set; }

    public int BranchId { get; set; }

    public TenantBranch Branch { get; set; } = null!;

    public int UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public DateOnly WorkDate { get; set; }

    public WorkLogType WorkType { get; set; }

    public string Narration { get; set; } = string.Empty;

    public int? CallsMade { get; set; }

    public int? CallsConnected { get; set; }

    public int? LeadsRespondedWell { get; set; }

    public int? FollowUpsScheduled { get; set; }

    public decimal? HoursSpent { get; set; }

    public string? FeaturesShipped { get; set; }

    public string? RepositoryPrLinks { get; set; }

    public string? BlockersEncountered { get; set; }

    public string Status { get; set; } = "Submitted";

    public string? ManagerNotes { get; set; }

    public int? ReviewedByManagerId { get; set; }
}
public class CustomerReceipt : BaseEntity, IBranchScopedEntity
{
    public int TenantId { get; set; }

    public int BranchId { get; set; }

    public TenantBranch Branch { get; set; } = null!;

    public string ReceiptNumber { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public decimal DepositAmount { get; set; }

    public string Currency { get; set; } = "USD";

    public string SlabTierName { get; set; } = string.Empty;

    public decimal AnnualYieldPercent { get; set; }

    public int LockinPeriodMonths { get; set; }

    public string PaymentMode { get; set; } = "Bank Wire";

    public string BankReferenceNumber { get; set; } = string.Empty;

    public string PayoutFrequency { get; set; } = "Monthly";

    public int IssuedByStaffId { get; set; }

    public ApplicationUser IssuedByStaff { get; set; } = null!;

    public string Status { get; set; } = "Confirmed";

    public string DigitalSecurityHash { get; set; } = string.Empty;
}
public class AttendanceRecord : BaseEntity, IBranchScopedEntity
{
    public int TenantId { get; set; }

    public int BranchId { get; set; }

    public TenantBranch Branch { get; set; } = null!;

    public int UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public DateOnly Date { get; set; }

    public TimeOnly? CheckInTime { get; set; }

    public TimeOnly? CheckOutTime { get; set; }

    public AttendanceStatus Status { get; set; }

    public decimal OvertimeHours { get; set; }

    public string? BiometricDeviceId { get; set; }
}
public class InvestmentSlab : BaseEntity, IMultiTenantEntity
{
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public decimal MonthlyRoiPercent { get; set; }
    public decimal AnnualYieldPercent { get; set; }
    public decimal StaffIncentivePercent { get; set; }
    public string Tagline { get; set; } = string.Empty;
}

public class KnowledgeVideo : BaseEntity, IMultiTenantEntity
{
    public int TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Instructor { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string[] KeyTakeaways { get; set; } = Array.Empty<string>();
}

public class AuditLog : BaseEntity, IMultiTenantEntity
{
    public int TenantId { get; set; }
    public int ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
}
