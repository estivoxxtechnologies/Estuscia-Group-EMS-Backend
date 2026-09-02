using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Common;
using Estuscia.Domain.Entities;
using Microsoft.EntityFrameworkCore;

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
            .HasIndex(e => e.Email)
            .IsUnique();

        modelBuilder.Entity<ApplicationUser>()
            .HasOne(e => e.Tenant)
            .WithMany(e => e.Users)
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

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
        // SalesStaff
        //     Sales       -> View/Create/Edit
        //     Attendance  -> View/Create
        //     Investment  -> No Access
        //
        // ========================================================

        modelBuilder.Entity<RoleModulePermission>()
            .HasIndex(e => new
            {
                e.TenantId,
                e.Role,
                e.ModuleId
            })
            .IsUnique();

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


        // ========================================================
        // KNOWLEDGE VIDEO
        // ========================================================

        modelBuilder.Entity<KnowledgeVideo>()
            .Property(e => e.KeyTakeaways)
            .HasConversion(
                v => string.Join("|||", v),
                v => v.Split(
                    new[] { "|||" },
                    StringSplitOptions.RemoveEmptyEntries));


        // ========================================================
        // ENUM STORAGE
        //
        // Keep enums as integers in SQL Server.
        // ========================================================

        modelBuilder.Entity<ApplicationUser>()
            .Property(e => e.Role)
            .HasConversion<int>();

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
        // --------------------------------------------------------
        // Automatically assign TenantId to new multi-tenant data
        // --------------------------------------------------------

// --------------------------------------------------------
// TENANT SECURITY
// --------------------------------------------------------

foreach (var entry in ChangeTracker.Entries<IMultiTenantEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                // SuperAdmin may create records with an explicitly
                // selected tenant through controlled server-side APIs.
                if (_tenantService.IsSuperAdmin)
                {
                    if (entry.Entity.TenantId == Guid.Empty &&
                        _tenantService.TenantId.HasValue)
                    {
                        entry.Entity.TenantId =
                            _tenantService.TenantId.Value;
                    }

                    continue;
                }

                // Normal users MUST have a tenant.
                if (!_tenantService.TenantId.HasValue)
                {
                    throw new InvalidOperationException(
                        "Authenticated user does not have a tenant.");
                }

                // Always force the entity to the authenticated user's
                // tenant. Never trust TenantId supplied by the client.
                entry.Entity.TenantId =
                    _tenantService.TenantId.Value;
            }

            // ----------------------------------------------------
            // PREVENT CROSS-TENANT MODIFICATION
            // ----------------------------------------------------

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


        // --------------------------------------------------------
        // Audit fields
        // --------------------------------------------------------

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc =
                    DateTime.UtcNow;

                if (_tenantService.UserId.HasValue)
                {
                    entry.Entity.CreatedByUserId =
                        _tenantService.UserId.Value.ToString();
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