using Estuscia.Application.Common.Interfaces;
using Estuscia.Application.Users.DTOs;
using Estuscia.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IAppDbContext _context;

    public UsersController(IAppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetUsers(
        CancellationToken cancellationToken)
    {
        var tenantIdValue = User.FindFirst("tenant_id")?.Value;

        if (!int.TryParse(tenantIdValue, out var tenantId))
        {
            return Unauthorized(new
            {
                message = "Tenant information is missing from the authenticated user."
            });
        }

        var users = await _context.Users
            .AsNoTracking()
            .Where(u =>
                u.TenantId == tenantId)
            .Include(u => u.Tenant)
            .Include(u => u.Branch)
            .Include(u => u.Role)
            .OrderBy(u => u.FullName)
            .Select(u => new UserDto
            {
                Id = u.Id,

                TenantId = u.TenantId,
                TenantName = u.Tenant != null
                    ? u.Tenant.Name
                    : string.Empty,

                BranchId = u.BranchId,
                BranchName = u.Branch != null
                    ? u.Branch.BranchName
                    : null,

                FullName = u.FullName,
                Email = u.Email,

                EmployeeCode = u.EmployeeCode,

                RoleId = u.RoleNumber,
                RoleName = u.Role != null
                    ? u.Role.RoleName
                    : string.Empty,

                Designation = u.Designation,
                Department = u.Department,

                SalaryBase = u.SalaryBase,

                AvatarUrl = u.AvatarUrl,

                IsActive = u.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(
    [FromBody] CreateUserDto request,
    CancellationToken cancellationToken)
    {
        var tenantIdValue = User.FindFirst("tenant_id")?.Value;

        if (!int.TryParse(tenantIdValue, out var tenantId))
        {
            return Unauthorized(new
            {
                message = "Tenant information is missing from the authenticated user."
            });
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new
            {
                message = "Full name is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new
            {
                message = "Email is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Password is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.EmployeeCode))
        {
            return BadRequest(new
            {
                message = "Employee code is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Designation))
        {
            return BadRequest(new
            {
                message = "Designation is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Department))
        {
            return BadRequest(new
            {
                message = "Department is required."
            });
        }

        var email = request.Email
            .Trim()
            .ToLowerInvariant();

        var employeeCode = request.EmployeeCode.Trim();

        // Check duplicate email inside this tenant
        var emailExists = await _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(
                u =>
                    u.TenantId == tenantId &&
                    u.Email.ToLower() == email,
                cancellationToken);

        if (emailExists)
        {
            return Conflict(new
            {
                message = "A user with this email already exists."
            });
        }

        // Check duplicate employee code inside this tenant
        var employeeCodeExists = await _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(
                u =>
                    u.TenantId == tenantId &&
                    u.EmployeeCode == employeeCode,
                cancellationToken);

        if (employeeCodeExists)
        {
            return Conflict(new
            {
                message = "A user with this employee code already exists."
            });
        }

        // Validate role
        var role = await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r =>
                    r.RoleNumber == request.RoleNumber &&
                    r.IsActive,
                cancellationToken);

        if (role == null)
        {
            return BadRequest(new
            {
                message = "Invalid or inactive role."
            });
        }

        // Validate branch
        TenantBranch? branch = null;

        if (request.BranchId.HasValue)
        {
            branch = await _context.TenantBranches
                .FirstOrDefaultAsync(
                    b =>
                        b.Id == request.BranchId.Value &&
                        b.TenantId == tenantId &&
                        b.IsActive,
                    cancellationToken);

            if (branch == null)
            {
                return BadRequest(new
                {
                    message = "Invalid branch for this organization."
                });
            }
        }

        var user = new ApplicationUser
        {
            TenantId = tenantId,
            BranchId = request.BranchId,

            FullName = request.FullName.Trim(),
            Email = email,

            PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                request.Password),

            EmployeeCode = employeeCode,

            RoleNumber = request.RoleNumber,

            Designation = request.Designation.Trim(),
            Department = request.Department.Trim(),

            SalaryBase = request.SalaryBase,

            AvatarUrl = request.AvatarUrl?.Trim() ?? string.Empty,

            IsActive = true
        };

        _context.Users.Add(user);

        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetUsers),
            new { id = user.Id },
            new
            {
                id = user.Id,
                message = "Employee created successfully."
            });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUser(
    int id,
    [FromBody] UpdateUserDto request,
    CancellationToken cancellationToken)
    {
        // Get tenant from JWT
        var tenantIdValue =
            User.FindFirst("tenant_id")?.Value
            ?? User.FindFirst("tenantId")?.Value;

        if (!int.TryParse(tenantIdValue, out var tenantId))
        {
            return Unauthorized(new
            {
                message = "Tenant information is missing from the authenticated user."
            });
        }

        // Validate request
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new
            {
                message = "Full name is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new
            {
                message = "Email is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.EmployeeCode))
        {
            return BadRequest(new
            {
                message = "Employee code is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Designation))
        {
            return BadRequest(new
            {
                message = "Designation is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Department))
        {
            return BadRequest(new
            {
                message = "Department is required."
            });
        }

        if (request.SalaryBase < 0)
        {
            return BadRequest(new
            {
                message = "Salary cannot be negative."
            });
        }

        // Find the employee inside the authenticated tenant
        var user = await _context.Users
            .Include(u => u.Tenant)
            .Include(u => u.Branch)
            .Include(u => u.Role)
            .FirstOrDefaultAsync(
                u =>
                    u.Id == id &&
                    u.TenantId == tenantId,
                cancellationToken);

        if (user == null)
        {
            return NotFound(new
            {
                message = "Employee not found."
            });
        }

        // Normalize values
        var email = request.Email
            .Trim()
            .ToLowerInvariant();

        var employeeCode = request.EmployeeCode.Trim();

        // Check duplicate email
        var emailExists = await _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(
                u =>
                    u.TenantId == tenantId &&
                    u.Id != id &&
                    u.Email.ToLower() == email,
                cancellationToken);

        if (emailExists)
        {
            return Conflict(new
            {
                message = "Another employee with this email already exists."
            });
        }

        // Check duplicate employee code
        var employeeCodeExists = await _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(
                u =>
                    u.TenantId == tenantId &&
                    u.Id != id &&
                    u.EmployeeCode == employeeCode,
                cancellationToken);

        if (employeeCodeExists)
        {
            return Conflict(new
            {
                message = "Another employee with this employee code already exists."
            });
        }

        // Validate role
        var role = await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r =>
                    r.RoleNumber == request.RoleNumber &&
                    r.IsActive,
                cancellationToken);

        if (role == null)
        {
            return BadRequest(new
            {
                message = "Invalid or inactive role."
            });
        }

        // Validate branch
        var branch = await _context.TenantBranches
            .FirstOrDefaultAsync(
                b =>
                    b.Id == request.BranchId &&
                    b.TenantId == tenantId &&
                    b.IsActive,
                cancellationToken);

        if (branch == null)
        {
            return BadRequest(new
            {
                message = "Invalid or inactive branch for this organization."
            });
        }

        // Update employee
        user.FullName = request.FullName.Trim();

        user.Email = email;

        user.EmployeeCode = employeeCode;

        user.RoleNumber = request.RoleNumber;

        user.Designation = request.Designation.Trim();

        user.Department = request.Department.Trim();

        user.SalaryBase = request.SalaryBase;

        user.BranchId = request.BranchId;

        // Keep TenantBranchId synchronized with BranchId
        user.BranchId = branch.Id;

        user.AvatarUrl =
            request.AvatarUrl?.Trim()
            ?? user.AvatarUrl
            ?? string.Empty;

        user.IsActive = request.IsActive;

        user.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Reload navigation properties so response contains
        // the latest branch and role information
        var updatedUser = await _context.Users
            .AsNoTracking()
            .Include(u => u.Tenant)
            .Include(u => u.Branch)
            .Include(u => u.Role)
            .FirstOrDefaultAsync(
                u =>
                    u.Id == id &&
                    u.TenantId == tenantId,
                cancellationToken);

        if (updatedUser == null)
        {
            return NotFound(new
            {
                message = "Employee was updated but could not be loaded."
            });
        }

        var response = new UserDto
        {
            Id = updatedUser.Id,

            TenantId = updatedUser.TenantId,
            TenantName = updatedUser.Tenant != null
                ? updatedUser.Tenant.Name
                : string.Empty,

            BranchId = updatedUser.BranchId,
            BranchName = updatedUser.Branch != null
                ? updatedUser.Branch.BranchName
                : null,

            FullName = updatedUser.FullName,
            Email = updatedUser.Email,

            EmployeeCode = updatedUser.EmployeeCode,

            RoleId = updatedUser.RoleNumber,
            RoleName = updatedUser.Role != null
                ? updatedUser.Role.RoleName
                : string.Empty,

            Designation = updatedUser.Designation,
            Department = updatedUser.Department,

            SalaryBase = updatedUser.SalaryBase,

            AvatarUrl = updatedUser.AvatarUrl,

            IsActive = updatedUser.IsActive
        };

        return Ok(response);
    }

    // ========================================================
    // CREATE COMPANY ADMIN
    // ========================================================

    [HttpPost("company-admin/{tenantId:int}")]
    public async Task<IActionResult> CreateCompanyAdmin(
        int tenantId,
        [FromBody] CreateCompanyAdminDto request,
        CancellationToken cancellationToken)
    {
        // ========================================================
        // CHECK CURRENT USER
        // ========================================================

        var currentUserIdValue =
            User.FindFirst("user_id")?.Value
            ?? User.FindFirst("sub")?.Value;

        if (!int.TryParse(currentUserIdValue, out var currentUserId))
            return Unauthorized();

        var currentUser = await _context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.Id == currentUserId,
                cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        // Only Super Admin can create Company Admin
        if (currentUser.RoleNumber != 1)
            return Forbid();

        // ========================================================
        // VALIDATE TENANT
        // ========================================================

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                t => t.Id == tenantId,
                cancellationToken);

        if (tenant == null)
        {
            return NotFound(new
            {
                message = "Tenant not found."
            });
        }

        if (!tenant.IsActive)
        {
            return BadRequest(new
            {
                message = "Cannot create a Company Admin for an inactive tenant."
            });
        }

        // ========================================================
        // VALIDATE BASIC INFORMATION
        // ========================================================

        var fullName = request.FullName.Trim();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest(new
            {
                message = "Full name is required."
            });
        }

        var emailValue = request.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(emailValue))
        {
            return BadRequest(new
            {
                message = "Email is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Password is required."
            });
        }

        var employeeCode = request.EmployeeCode.Trim();

        if (string.IsNullOrWhiteSpace(employeeCode))
        {
            return BadRequest(new
            {
                message = "Employee code is required."
            });
        }

        var designation = request.Designation.Trim();

        if (string.IsNullOrWhiteSpace(designation))
        {
            return BadRequest(new
            {
                message = "Designation is required."
            });
        }

        var department = request.Department.Trim();

        if (string.IsNullOrWhiteSpace(department))
        {
            return BadRequest(new
            {
                message = "Department is required."
            });
        }

        // ========================================================
        // COMPANY ADMIN ROLE
        // ========================================================

        // RoleNumber 2 = company_admin
        const int companyAdminRoleNumber = 2;

        var role = await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r =>
                    r.RoleNumber == companyAdminRoleNumber &&
                    r.IsActive,
                cancellationToken);

        if (role == null)
        {
            return StatusCode(500, new
            {
                message = "Company Admin role is not configured."
            });
        }

        // ========================================================
        // CHECK DUPLICATE EMAIL
        // ========================================================

        var emailExists = await _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(
                u =>
                    u.TenantId == tenantId &&
                    u.Email == emailValue,
                cancellationToken);

        if (emailExists)
        {
            return Conflict(new
            {
                message =
                    "A user with this email already exists in this tenant."
            });
        }

        // ========================================================
        // CHECK DUPLICATE EMPLOYEE CODE
        // ========================================================

        var employeeCodeExists = await _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(
                u =>
                    u.TenantId == tenantId &&
                    u.EmployeeCode == employeeCode,
                cancellationToken);

        if (employeeCodeExists)
        {
            return Conflict(new
            {
                message =
                    "A user with this employee code already exists in this tenant."
            });
        }

        // ========================================================
        // CREATE COMPANY ADMIN
        // ========================================================

        var user = new ApplicationUser
        {
            // Selected tenant
            TenantId = tenantId,

            // IMPORTANT:
            // Company Admin is tenant-level,
            // NOT branch-level.
            BranchId = null,

            FullName = fullName,

            Email = emailValue,

            PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    request.Password),

            EmployeeCode = employeeCode,

            // ALWAYS company_admin
            RoleNumber = companyAdminRoleNumber,

            Designation = designation,

            Department = department,

            SalaryBase = request.SalaryBase,

            AvatarUrl =
                request.AvatarUrl?.Trim()
                ?? string.Empty,

            IsActive = true
        };

        _context.Users.Add(user);

        await _context.SaveChangesAsync(
            cancellationToken);

        // ========================================================
        // RESPONSE
        // ========================================================

        return Created(
            string.Empty,
            new
            {
                id = user.Id,

                message =
                    "Company Admin created successfully.",

                tenantId = user.TenantId,

                tenantName = tenant.Name,

                // Always null for Company Admin
                branchId = (int?)null,

                fullName = user.FullName,

                email = user.Email,

                employeeCode = user.EmployeeCode,

                roleNumber = user.RoleNumber,

                roleName = role.RoleName,

                designation = user.Designation,

                department = user.Department,

                salaryBase = user.SalaryBase,

                avatarUrl = user.AvatarUrl,

                isActive = user.IsActive
            });
    }

    [HttpGet("company-admins/{tenantId:int}")]
    public async Task<IActionResult> GetCompanyAdmins(
    int tenantId,
    CancellationToken cancellationToken)
    {
        // ---------------------------------------------------------
        // Only SuperAdmin can manage Company Admins
        // ---------------------------------------------------------
        var currentUserIdValue =
            User.FindFirst("user_id")?.Value
            ?? User.FindFirst("sub")?.Value;

        if (!int.TryParse(currentUserIdValue, out var currentUserId))
        {
            return Unauthorized(new
            {
                message = "Authenticated user information is missing."
            });
        }

        var currentUser = await _context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.Id == currentUserId,
                cancellationToken);

        if (currentUser == null)
        {
            return Unauthorized(new
            {
                message = "Authenticated user was not found."
            });
        }

        // Role 1 = Super Admin
        if (currentUser.RoleNumber != 1)
        {
            return Forbid();
        }

        // ---------------------------------------------------------
        // Validate tenant
        // ---------------------------------------------------------
        var tenantExists = await _context.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                t => t.Id == tenantId,
                cancellationToken);

        if (!tenantExists)
        {
            return NotFound(new
            {
                message = "Tenant not found."
            });
        }

        // ---------------------------------------------------------
        // Get Company Admins for selected tenant
        // Role 2 = Company Admin
        // ---------------------------------------------------------
        var admins = await _context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(u => u.Tenant)
            .Include(u => u.Branch)
            .Include(u => u.Role)
            .Where(u =>
                u.TenantId == tenantId &&
                u.RoleNumber == 2)
            .OrderBy(u => u.FullName)
            .Select(u => new UserDto
            {
                Id = u.Id,

                TenantId = u.TenantId,

                TenantName = u.Tenant != null
                    ? u.Tenant.Name
                    : string.Empty,

                BranchId = u.BranchId,

                BranchName = u.Branch != null
                    ? u.Branch.BranchName
                    : null,

                FullName = u.FullName,

                Email = u.Email,

                EmployeeCode = u.EmployeeCode,

                RoleId = u.RoleNumber,

                RoleName = u.Role != null
                    ? u.Role.RoleName
                    : string.Empty,

                Designation = u.Designation,

                Department = u.Department,

                SalaryBase = u.SalaryBase,

                AvatarUrl = u.AvatarUrl,

                IsActive = u.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(admins);
    }
}