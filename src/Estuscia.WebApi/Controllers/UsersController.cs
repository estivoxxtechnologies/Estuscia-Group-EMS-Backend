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
                u.TenantId == tenantId &&
                u.IsActive)
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
}