using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Common;
using Estuscia.Domain.Entities;
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
    public DbSet<InvestmentSlab> InvestmentSlabs =>
        Set<InvestmentSlab>();
    public DbSet<KnowledgeVideo> KnowledgeVideos =>
        Set<KnowledgeVideo>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();


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

        modelBuilder.Entity<DailyWorkLog>()
            .HasIndex(e => new
            {
                e.TenantId,
                e.BranchId,
                e.WorkDate
            });

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
