using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Common;
using Estuscia.Domain.Entities;
using Estuscia.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;


namespace Estuscia.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    private readonly ICurrentTenantService _tenantService;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentTenantService tenantService)
        : base(options)
    {
        _tenantService = tenantService;
    }

    // ============================================================
    // TENANT / USER
    // ============================================================

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantBranch> TenantBranches => Set<TenantBranch>();
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Currency> Currencies => Set<Currency>();

    // ============================================================
    // PERMISSIONS
    // ============================================================

    public DbSet<Module> Modules => Set<Module>();
    public DbSet<RoleModulePermission> RoleModulePermissions =>
        Set<RoleModulePermission>();
    public DbSet<UserModulePermission> UserModulePermissions =>
        Set<UserModulePermission>();

    // ============================================================
    // EMS MODULES
    // ============================================================

    public DbSet<DailyWorkLog> DailyWorkLogs => Set<DailyWorkLog>();
    public DbSet<CustomerReceipt> CustomerReceipts =>
        Set<CustomerReceipt>();
    public DbSet<AttendanceRecord> AttendanceRecords =>
        Set<AttendanceRecord>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<InvestmentSlab> InvestmentSlabs =>
        Set<InvestmentSlab>();
    public DbSet<KnowledgeVideo> KnowledgeVideos =>
        Set<KnowledgeVideo>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<TenantPayment> TenantPayments { get; set; }
    public DbSet<SalesLead> SalesLeads => Set<SalesLead>();
    public DbSet<SalesLeadAssignment> SalesLeadAssignments =>
        Set<SalesLeadAssignment>();
    public DbSet<DeveloperWork> DeveloperWorks =>
    Set<DeveloperWork>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ========================================================
        // TENANT
        // ========================================================

        modelBuilder.Entity<Tenant>()
            .HasIndex(e => e.Code)
            .IsUnique();

        // ========================================================
        // TENANT BRANCH
        // ========================================================

        modelBuilder.Entity<TenantBranch>()
            .HasKey(e => e.Id);

        modelBuilder.Entity<TenantBranch>()
            .HasIndex(e => new
            {
                e.TenantId,
                e.BranchName
            })
            .IsUnique();

        modelBuilder.Entity<TenantBranch>()
            .HasAlternateKey(e => new
            {
                e.TenantId,
                e.Id
            });

        modelBuilder.Entity<TenantBranch>()
            .HasOne(e => e.Tenant)
            .WithMany(e => e.Branches)
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Customer Receipt -> Currency
        modelBuilder.Entity<CustomerReceipt>()
            .HasOne(x => x.Currency)
            .WithMany()
            .HasForeignKey(x => x.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Investment Slab -> Currency
        modelBuilder.Entity<InvestmentSlab>()
            .HasOne(x => x.Currency)
            .WithMany()
            .HasForeignKey(x => x.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Tenant Payment -> Currency
        modelBuilder.Entity<TenantPayment>()
            .HasOne(x => x.Currency)
            .WithMany()
            .HasForeignKey(x => x.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);


        // ========================================================
        // APPLICATION USER
        // ========================================================

        modelBuilder.Entity<ApplicationUser>()
    .HasIndex(e => new
    {
        e.TenantId,
        e.Email
    })
    .IsUnique();

        modelBuilder.Entity<ApplicationUser>()
    .HasIndex(e => new
    {
        e.TenantId,
        e.EmployeeCode
    })
    .IsUnique();

        modelBuilder.Entity<ApplicationUser>()
    .HasOne(e => e.Tenant)
    .WithMany(e => e.Users)
    .HasForeignKey(e => e.TenantId)
    .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ApplicationUser>()
            .HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => new
            {
                e.TenantId,
                e.BranchId
            })
            .HasPrincipalKey(e => new
            {
                e.TenantId,
                e.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ApplicationUser>()
            .HasOne(e => e.Role)
            .WithMany(e => e.Users)
            .HasForeignKey(e => e.RoleNumber)
            .HasPrincipalKey(e => e.RoleNumber)
            .OnDelete(DeleteBehavior.Restrict);

        // ========================================================
        // ROLE
        //
        // GLOBAL STATIC MASTER DATA
        //
        // Roles are NOT tenant-specific.
        // They are seeded through EF Core migrations.
        //
        // ========================================================

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.RoleNumber)
                .IsRequired();

            entity.Property(e => e.RoleName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.DisplayName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(e => e.IsActive)
                .IsRequired();

            entity.HasIndex(e => e.RoleNumber)
                .IsUnique();

            entity.HasIndex(e => e.RoleName)
                .IsUnique();
        });

        //TENANT PAYMENT
        modelBuilder.Entity<TenantPayment>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Amount)
                .HasPrecision(18, 2);

            entity.Property(x => x.PaymentMode)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(x => x.PaymentStatus)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(x => x.RegistrationStatus)
                .IsRequired();

            entity.Property(x => x.TotalBranches)
                .IsRequired();

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            /*
             * Used when finding payment history/current coverage.
             */
            entity.HasIndex(x => x.TenantId);

            entity.HasIndex(x => new
            {
                x.TenantId,
                x.ValidFromUtc,
                x.ValidUntilUtc
            })
            .IsUnique();

            entity.HasIndex(x => new
            {
                x.TenantId,
                x.PaymentStatus
            });

            entity.HasIndex(x => new
            {
                x.TenantId,
                x.ValidUntilUtc
            });
        });

        //====================
        //Currency
        //====================

        modelBuilder.Entity<Currency>(entity =>
        {
            entity.ToTable("Currencies");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Code)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Symbol)
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(x => x.IsActive)
                .IsRequired();

            entity.HasIndex(x => x.Code)
                .IsUnique();
        });

        // ========================================================
        // CURRENCY RELATIONSHIPS
        // ========================================================

        // Tenant -> Default Currency
        modelBuilder.Entity<Tenant>()
            .HasOne(x => x.DefaultCurrency)
            .WithMany(x => x.Tenants)
            .HasForeignKey(x => x.DefaultCurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Branch -> Currency
        modelBuilder.Entity<TenantBranch>()
            .HasOne(x => x.Currency)
            .WithMany(x => x.Branches)
            .HasForeignKey(x => x.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        // ========================================================
        // STATIC CURRENCY DATA
        // ========================================================

        var currencyCreatedAt = new DateTime(
            2026,
            1,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        modelBuilder.Entity<Currency>().HasData(
            new Currency
            {
                Id = 1,
                Code = "USD",
                Name = "US Dollar",
                Symbol = "$",
                IsActive = true,
                CreatedAtUtc = currencyCreatedAt
            },
            new Currency
            {
                Id = 2,
                Code = "INR",
                Name = "Indian Rupee",
                Symbol = "₹",
                IsActive = true,
                CreatedAtUtc = currencyCreatedAt
            },
            new Currency
            {
                Id = 3,
                Code = "AED",
                Name = "United Arab Emirates Dirham",
                Symbol = "د.إ",
                IsActive = true,
                CreatedAtUtc = currencyCreatedAt
            },
            new Currency
            {
                Id = 4,
                Code = "EUR",
                Name = "Euro",
                Symbol = "€",
                IsActive = true,
                CreatedAtUtc = currencyCreatedAt
            },
            new Currency
            {
                Id = 5,
                Code = "GBP",
                Name = "British Pound",
                Symbol = "£",
                IsActive = true,
                CreatedAtUtc = currencyCreatedAt
            },
            new Currency
            {
                Id = 6,
                Code = "SAR",
                Name = "Saudi Riyal",
                Symbol = "﷼",
                IsActive = true,
                CreatedAtUtc = currencyCreatedAt
            }
        );

        // ========================================================
        // STATIC ROLE DATA
        //
        // These records are managed by EF Core migrations.
        // Role IDs are fixed integer values and must not change.
        //
        // RoleNumber is the stable business role identifier.
        // Id is the database primary key.
        //
        // ========================================================

        var roleCreatedAt = new DateTime(
            2026,
            1,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        modelBuilder.Entity<Role>().HasData(
            new Role
            {
                Id = 1,
                RoleNumber = 1,
                RoleName = "super_admin",
                DisplayName = "Super Admin",
                IsActive = true,
                CreatedAtUtc = roleCreatedAt
            },

            new Role
            {
                Id = 2,
                RoleNumber = 2,
                RoleName = "company_admin",
                DisplayName = "Company Admin",
                IsActive = true,
                CreatedAtUtc = roleCreatedAt
            },

            new Role
            {
                Id = 3,
                RoleNumber = 3,
                RoleName = "hr_ops",
                DisplayName = "HR Operations",
                IsActive = true,
                CreatedAtUtc = roleCreatedAt
            },

            new Role
            {
                Id = 4,
                RoleNumber = 4,
                RoleName = "branch_manager",
                DisplayName = "Branch Manager",
                IsActive = true,
                CreatedAtUtc = roleCreatedAt
            },

            new Role
            {
                Id = 5,
                RoleNumber = 5,
                RoleName = "sales_staff",
                DisplayName = "Sales Staff",
                IsActive = true,
                CreatedAtUtc = roleCreatedAt
            },

            new Role
            {
                Id = 6,
                RoleNumber = 6,
                RoleName = "developer",
                DisplayName = "Developer",
                IsActive = true,
                CreatedAtUtc = roleCreatedAt
            },

            new Role
            {
                Id = 7,
                RoleNumber = 7,
                RoleName = "support_staff",
                DisplayName = "Support Staff",
                IsActive = true,
                CreatedAtUtc = roleCreatedAt
            },

            new Role
            {
                Id = 8,
                RoleNumber = 8,
                RoleName = "knowledge_trainer",
                DisplayName = "Knowledge Trainer",
                IsActive = true,
                CreatedAtUtc = roleCreatedAt
            }
        );

        // ========================================================
        // MODULE
        // ========================================================

        modelBuilder.Entity<Module>()
            .HasIndex(e => e.Code)
            .IsUnique();


        // ========================================================
        // ROLE MODULE PERMISSION
        //
        // Defines DEFAULT permissions for a role.
        //
        // Example:
        //
        // RoleNumber 5 = sales_staff
        //
        // Sales       -> View/Create/Edit
        // Attendance  -> View/Create
        // Investment  -> No Access
        //
        // ========================================================

        modelBuilder.Entity<RoleModulePermission>()
            .HasIndex(e => new
            {
                e.TenantId,
                e.RoleNumber,
                e.ModuleId
            })
            .IsUnique();

        modelBuilder.Entity<RoleModulePermission>()
            .HasOne(e => e.Role)
            .WithMany()
            .HasForeignKey(e => e.RoleNumber)
            .HasPrincipalKey(e => e.RoleNumber)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RoleModulePermission>()
            .HasOne(e => e.Module)
            .WithMany()
            .HasForeignKey(e => e.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);


        // ========================================================
        // USER MODULE PERMISSION
        //
        // Defines USER-SPECIFIC OVERRIDES.
        //
        // null  = use role default
        // true  = explicitly allow
        // false = explicitly deny
        //
        // ========================================================

        modelBuilder.Entity<UserModulePermission>()
            .HasIndex(e => new
            {
                e.TenantId,
                e.UserId,
                e.ModuleId
            })
            .IsUnique();

        modelBuilder.Entity<UserModulePermission>()
            .HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserModulePermission>()
            .HasOne(e => e.Module)
            .WithMany()
            .HasForeignKey(e => e.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);


        // ========================================================
        // DAILY WORK LOG
        // ========================================================

        // General query/index support
        modelBuilder.Entity<DailyWorkLog>()
            .HasIndex(e => new
            {
                e.TenantId,
                e.BranchId,
                e.WorkDate
            });

        // One daily report per user + work type + date
        modelBuilder.Entity<DailyWorkLog>()
            .HasIndex(e => new
            {
                e.TenantId,
                e.UserId,
                e.WorkDate,
                e.WorkType
            })
            .IsUnique();

        modelBuilder.Entity<DailyWorkLog>()
            .HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DailyWorkLog>()
            .HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => new
            {
                e.TenantId,
                e.BranchId
            })
            .HasPrincipalKey(e => new
            {
                e.TenantId,
                e.Id
            })
            .OnDelete(DeleteBehavior.Restrict);


        // ========================================================
        // SALES LEADS
        // ========================================================

        modelBuilder.Entity<SalesLead>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.PhoneNumber)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.CustomerName)
                .HasMaxLength(200);

            entity.Property(x => x.IsActive)
                .IsRequired();

            // Tenant + phone number lookup
            entity.HasIndex(x => new
            {
                x.TenantId,
                x.PhoneNumber
            });

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });


        // ========================================================
        // SALES LEAD ASSIGNMENTS
        // ========================================================

        modelBuilder.Entity<SalesLeadAssignment>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Outcome)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(x => x.Notes)
                .HasMaxLength(1000);

            entity.Property(x => x.AssignedAtUtc)
                .IsRequired();

            // Fast lookup for employee's leads/outcomes
            entity.HasIndex(x => new
            {
                x.TenantId,
                x.AssignedToUserId,
                x.Outcome
            });

            // Fast branch/team/date lookup
            entity.HasIndex(x => new
            {
                x.TenantId,
                x.BranchId,
                x.AssignedToUserId,
                x.AssignedAtUtc
            });

            entity.HasOne(x => x.SalesLead)
                .WithMany(x => x.Assignments)
                .HasForeignKey(x => x.SalesLeadId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.AssignedToUser)
                .WithMany()
                .HasForeignKey(x => x.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssignedByUser)
                .WithMany()
                .HasForeignKey(x => x.AssignedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================
        // DEVELOPER WORK
        // ============================================================

        modelBuilder.Entity<DeveloperWork>()
            .Property(x => x.Status)
            .HasConversion<int>();

        modelBuilder.Entity<DeveloperWork>()
            .Property(x => x.Priority)
            .HasConversion<int>();

        modelBuilder.Entity<DeveloperWork>()
            .Property(x => x.WorkType)
            .HasConversion<int>();

        // ------------------------------------------------------------
        // Tenant relationship
        // ------------------------------------------------------------

        modelBuilder.Entity<DeveloperWork>()
            .HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // ------------------------------------------------------------
        // Branch relationship
        // ------------------------------------------------------------

        modelBuilder.Entity<DeveloperWork>()
            .HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // ------------------------------------------------------------
        // Assigned To
        // ------------------------------------------------------------

        modelBuilder.Entity<DeveloperWork>()
            .HasOne(x => x.AssignedToUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ------------------------------------------------------------
        // Assigned By
        // ------------------------------------------------------------

        modelBuilder.Entity<DeveloperWork>()
            .HasOne(x => x.AssignedByUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ------------------------------------------------------------
        // Indexes
        // ------------------------------------------------------------

        modelBuilder.Entity<DeveloperWork>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.AssignedToUserId,
                x.Status
            });

        modelBuilder.Entity<DeveloperWork>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.BranchId,
                x.Status
            });

        modelBuilder.Entity<DeveloperWork>()
            .HasIndex(x => new
            {
                x.TenantId,
                x.AssignedToUserId,
                x.DueDateUtc
            });


        // ========================================================
        // CUSTOMER RECEIPT
        // ========================================================

        modelBuilder.Entity<CustomerReceipt>()
            .HasIndex(e => new
            {
                e.TenantId,
                e.ReceiptNumber
            })
            .IsUnique();

        modelBuilder.Entity<CustomerReceipt>()
            .HasOne(e => e.IssuedByStaff)
            .WithMany()
            .HasForeignKey(e => e.IssuedByStaffId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CustomerReceipt>()
            .HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => new
            {
                e.TenantId,
                e.BranchId
            })
            .HasPrincipalKey(e => new
            {
                e.TenantId,
                e.Id
            })
            .OnDelete(DeleteBehavior.Restrict);


        // ========================================================
        // ATTENDANCE
        // ========================================================

        modelBuilder.Entity<AttendanceRecord>()
            .HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AttendanceRecord>()
            .HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => new
            {
                e.TenantId,
                e.BranchId
            })
            .HasPrincipalKey(e => new
            {
                e.TenantId,
                e.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AttendanceRecord>()
            .HasIndex(e => new
            {
                e.TenantId,
                e.BranchId,
                e.UserId,
                e.Date
            });

        // ========================================================
        // LEAVE REQUEST
        // ========================================================

        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.ToTable("LeaveRequests");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            // ----------------------------------------------------
            // ENUMS
            // ----------------------------------------------------

            entity.Property(e => e.LeaveType)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(e => e.Status)
                .HasConversion<int>()
                .IsRequired();

            // ----------------------------------------------------
            // DATES
            // ----------------------------------------------------

            entity.Property(e => e.StartDate)
                .HasColumnType("date")
                .IsRequired();

            entity.Property(e => e.EndDate)
                .HasColumnType("date")
                .IsRequired();

            // ----------------------------------------------------
            // REQUESTED DAYS
            // ----------------------------------------------------

            entity.Property(e => e.RequestedDays)
                .HasPrecision(10, 2)
                .IsRequired();

            // ----------------------------------------------------
            // REASON
            // ----------------------------------------------------

            entity.Property(e => e.Reason)
                .HasMaxLength(2000)
                .IsRequired();

            // ----------------------------------------------------
            // MEDICAL CERTIFICATE
            // ----------------------------------------------------

            entity.Property(e => e.MedicalCertificateFileUrl)
                .HasMaxLength(1000);

            entity.Property(e => e.MedicalCertificateFileName)
                .HasMaxLength(255);

            // ----------------------------------------------------
            // HR REVIEW
            // ----------------------------------------------------

            entity.Property(e => e.ReviewReason)
                .HasMaxLength(2000);

            // ----------------------------------------------------
            // AUDIT
            // ----------------------------------------------------

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired();

            // ----------------------------------------------------
            // TENANT
            // ----------------------------------------------------

            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // ----------------------------------------------------
            // BRANCH
            //
            // TenantId + BranchId
            // -> TenantBranches(TenantId + Id)
            // ----------------------------------------------------

            entity.HasOne(e => e.Branch)
                .WithMany()
                .HasForeignKey(e => new
                {
                    e.TenantId,
                    e.BranchId
                })
                .HasPrincipalKey(e => new
                {
                    e.TenantId,
                    e.Id
                })
                .OnDelete(DeleteBehavior.Restrict);

            // ----------------------------------------------------
            // EMPLOYEE
            // ----------------------------------------------------

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ----------------------------------------------------
            // HR REVIEWED BY
            // ----------------------------------------------------

            entity.HasOne(e => e.ReviewedByUser)
                .WithMany()
                .HasForeignKey(e => e.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ----------------------------------------------------
            // CREATED BY
            // ----------------------------------------------------

            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ----------------------------------------------------
            // INDEXES
            // ----------------------------------------------------

            entity.HasIndex(e => new
            {
                e.TenantId,
                e.BranchId,
                e.UserId,
                e.StartDate,
                e.EndDate
            });

            entity.HasIndex(e => new
            {
                e.TenantId,
                e.Status
            });

            entity.HasIndex(e => new
            {
                e.TenantId,
                e.BranchId,
                e.Status
            });
        });

        // ========================================================
        // MULTI-TENANT QUERY FILTERS
        // ========================================================

        modelBuilder.Entity<TenantBranch>()
            .HasQueryFilter(e =>
                _tenantService.IsSuperAdmin ||
                e.TenantId == _tenantService.TenantId);

        modelBuilder.Entity<ApplicationUser>()
            .HasQueryFilter(e =>
                _tenantService.IsSuperAdmin ||
                e.TenantId == _tenantService.TenantId);

        modelBuilder.Entity<RoleModulePermission>()
            .HasQueryFilter(e =>
                _tenantService.IsSuperAdmin ||
                e.TenantId == _tenantService.TenantId);

        modelBuilder.Entity<UserModulePermission>()
            .HasQueryFilter(e =>
                _tenantService.IsSuperAdmin ||
                e.TenantId == _tenantService.TenantId);

        modelBuilder.Entity<DailyWorkLog>()
            .HasQueryFilter(e =>
                _tenantService.IsSuperAdmin ||
                e.TenantId == _tenantService.TenantId);

        modelBuilder.Entity<CustomerReceipt>()
            .HasQueryFilter(e =>
                _tenantService.IsSuperAdmin ||
                e.TenantId == _tenantService.TenantId);

        modelBuilder.Entity<AttendanceRecord>()
            .HasQueryFilter(e =>
                _tenantService.IsSuperAdmin ||
                e.TenantId == _tenantService.TenantId);

        modelBuilder.Entity<LeaveRequest>()
            .HasQueryFilter(e =>
                _tenantService.IsSuperAdmin ||
                e.TenantId == _tenantService.TenantId);

        modelBuilder.Entity<InvestmentSlab>()
            .HasQueryFilter(e =>
                _tenantService.IsSuperAdmin ||
                e.TenantId == _tenantService.TenantId);

        modelBuilder.Entity<KnowledgeVideo>()
            .HasQueryFilter(e =>
                _tenantService.IsSuperAdmin ||
                e.TenantId == _tenantService.TenantId);

        modelBuilder.Entity<AuditLog>()
            .HasQueryFilter(e =>
                _tenantService.IsSuperAdmin ||
                e.TenantId == _tenantService.TenantId);

        // NEW
        modelBuilder.Entity<SalesLead>()
            .HasQueryFilter(e =>
                _tenantService.IsSuperAdmin ||
                e.TenantId == _tenantService.TenantId);

        modelBuilder.Entity<SalesLeadAssignment>()
            .HasQueryFilter(e =>
                _tenantService.IsSuperAdmin ||
                e.TenantId == _tenantService.TenantId);

        // ============================================================
        // DECIMAL PRECISION
        // ============================================================

        modelBuilder.Entity<ApplicationUser>()
            .Property(e => e.SalaryBase)
            .HasPrecision(18, 2);

        modelBuilder.Entity<DailyWorkLog>()
            .Property(e => e.HoursSpent)
            .HasPrecision(10, 2);

        modelBuilder.Entity<CustomerReceipt>()
            .Property(e => e.DepositAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<CustomerReceipt>()
            .Property(e => e.AnnualYieldPercent)
            .HasPrecision(8, 4);

        modelBuilder.Entity<AttendanceRecord>()
            .Property(e => e.OvertimeHours)
            .HasPrecision(10, 2);

        modelBuilder.Entity<InvestmentSlab>()
            .Property(e => e.MinAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<InvestmentSlab>()
            .Property(e => e.MaxAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<InvestmentSlab>()
            .Property(e => e.MonthlyRoiPercent)
            .HasPrecision(8, 4);

        modelBuilder.Entity<InvestmentSlab>()
            .Property(e => e.AnnualYieldPercent)
            .HasPrecision(8, 4);

        modelBuilder.Entity<InvestmentSlab>()
            .Property(e => e.StaffIncentivePercent)
            .HasPrecision(8, 4);

        // ========================================================
        // KNOWLEDGE VIDEO
        // ========================================================

        modelBuilder.Entity<KnowledgeVideo>()
    .Property(e => e.KeyTakeaways)
    .HasConversion(
        v => string.Join("|||", v),
        v => v.Split("|||", StringSplitOptions.None))
    .Metadata.SetValueComparer(
        new ValueComparer<string[]>(
            (a, b) =>
                a != null &&
                b != null &&
                a.SequenceEqual(b),

            v =>
                v == null
                    ? 0
                    : v.Aggregate(
                        0,
                        (hash, item) =>
                            HashCode.Combine(hash, item.GetHashCode())),

            v =>
                v == null
                    ? Array.Empty<string>()
                    : v.ToArray()
        ));


        // ========================================================
        // ENUM STORAGE
        //
        // Keep enums as integers in SQL Server.
        // ========================================================

        modelBuilder.Entity<DailyWorkLog>()
            .Property(e => e.WorkType)
            .HasConversion<int>();

        modelBuilder.Entity<AttendanceRecord>()
            .Property(e => e.Status)
            .HasConversion<int>();
    }


    // ============================================================
    // SAVE CHANGES
    // ============================================================

    public override Task<int> SaveChangesAsync(
    CancellationToken cancellationToken = default)
    {
        // ========================================================
        // TENANT SECURITY
        // ========================================================

        foreach (var entry in ChangeTracker.Entries<IMultiTenantEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                // ------------------------------------------------
                // SUPER ADMIN
                // ------------------------------------------------

                if (_tenantService.IsSuperAdmin)
                {
                    // If a server-side operation has explicitly
                    // selected a tenant, keep that TenantId.
                    //
                    // Otherwise use the current tenant context.
                    if (entry.Entity.TenantId == 0 &&
                        _tenantService.TenantId.HasValue)
                    {
                        entry.Entity.TenantId =
                            _tenantService.TenantId.Value;
                    }

                    continue;
                }

                // ------------------------------------------------
                // NORMAL USER
                // ------------------------------------------------

                if (!_tenantService.TenantId.HasValue)
                {
                    throw new InvalidOperationException(
                        "Authenticated user does not have a tenant.");
                }

                // NEVER trust TenantId from the client.
                entry.Entity.TenantId =
                    _tenantService.TenantId.Value;
            }

            // ====================================================
            // PREVENT CROSS-TENANT UPDATE / DELETE
            // ====================================================

            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                if (_tenantService.IsSuperAdmin)
                    continue;

                if (!_tenantService.TenantId.HasValue)
                {
                    throw new InvalidOperationException(
                        "Authenticated user does not have a tenant.");
                }

                if (entry.Entity.TenantId !=
                    _tenantService.TenantId.Value)
                {
                    throw new UnauthorizedAccessException(
                        "Cross-tenant data modification is not allowed.");
                }
            }
        }

        // ========================================================
        // AUDIT FIELDS
        // ========================================================

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc =
                    DateTime.UtcNow;

                if (_tenantService.UserId.HasValue)
                {
                    entry.Entity.CreatedByUserId =
                        _tenantService.UserId.Value;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc =
                    DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
