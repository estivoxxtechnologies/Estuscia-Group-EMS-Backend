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

public class TenantBranch : BaseEntity, IMultiTenantEntity
{
    public Guid TenantId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string? City { get; set; }
}

public class ApplicationUser : BaseEntity, IBranchScopedEntity
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string BranchName { get; set; } = string.Empty;
    
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string Designation { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal SalaryBase { get; set; }
    public string AvatarUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class DailyWorkLog : BaseEntity, IBranchScopedEntity
{
    public Guid TenantId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    
    public DateOnly WorkDate { get; set; }
    public WorkLogType WorkType { get; set; }
    public string Narration { get; set; } = string.Empty;
    
    // Sales calls metrics
    public int? CallsMade { get; set; }
    public int? CallsConnected { get; set; }
    public int? LeadsRespondedWell { get; set; }
    public int? FollowUpsScheduled { get; set; }
    
    // Software developer sprint metrics
    public decimal? HoursSpent { get; set; }
    public string? FeaturesShipped { get; set; }
    public string? RepositoryPrLinks { get; set; }
    public string? BlockersEncountered { get; set; }
    
    public string Status { get; set; } = "Submitted"; // Submitted, Reviewed, Verified
    public string? ManagerNotes { get; set; }
    public Guid? ReviewedByManagerId { get; set; }
}

public class CustomerReceipt : BaseEntity, IBranchScopedEntity
{
    public Guid TenantId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    
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
    
    public Guid IssuedByStaffId { get; set; }
    public ApplicationUser IssuedByStaff { get; set; } = null!;
    
    public string Status { get; set; } = "Confirmed";
    public string DigitalSecurityHash { get; set; } = string.Empty;
}

public class AttendanceRecord : BaseEntity, IBranchScopedEntity
{
    public Guid TenantId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    
    public Guid UserId { get; set; }
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
    public Guid TenantId { get; set; }
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
    public Guid TenantId { get; set; }
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
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
}
