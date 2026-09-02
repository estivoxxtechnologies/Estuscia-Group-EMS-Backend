using Estuscia.Application.Common.DTOs;
using Estuscia.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAppDbContext _context;
    private readonly IJwtTokenGenerator _jwtGenerator;

    public AuthController(
        IAppDbContext context,
        IJwtTokenGenerator jwtGenerator)
    {
        _context = context;
        _jwtGenerator = jwtGenerator;
    }

    // ============================================================
    // LOGIN
    // ============================================================

[HttpPost("login")]
[AllowAnonymous]
public async Task<IActionResult> Login(
    [FromBody] LoginRequestDto request)
    {
        // ============================================================
        // VALIDATE REQUEST
        // ============================================================

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Email and password are required."
            });
        }

        var email = request.Email
            .Trim()
            .ToLowerInvariant();


        // ============================================================
        // FIND USER
        //
        // IgnoreQueryFilters() is required here because we don't
        // know the tenant until we identify the user.
        // ============================================================

        var user =
            await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.Tenant)
                .Include(u => u.Branch)
                .FirstOrDefaultAsync(
                    u => u.Email.ToLower() == email);


        // ============================================================
        // INVALID USER
        // ============================================================

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }


        // ============================================================
        // ACCOUNT STATUS
        // ============================================================

        if (!user.IsActive)
        {
            return Unauthorized(new
            {
                message =
                    "Your account is inactive. Please contact an administrator."
            });
        }


        // ============================================================
        // PASSWORD VERIFICATION
        // ============================================================

        if (!BCrypt.Net.BCrypt.Verify(
                request.Password,
                user.PasswordHash))
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }


        // ============================================================
        // TENANT VALIDATION
        // ============================================================

        if (user.Tenant == null)
        {
            return Unauthorized(new
            {
                message =
                    "Your account is not associated with an organization."
            });
        }

        if (!user.Tenant.IsActive)
        {
            return Unauthorized(new
            {
                message = "This organization is inactive."
            });
        }


        // ============================================================
        // BRANCH VALIDATION
        //
        // Every branch-scoped user must have a valid branch.
        // ============================================================

        if (user.Branch == null)
        {
            return Unauthorized(new
            {
                message =
                    "Your account is not associated with a valid branch."
            });
        }

        if (!user.Branch.IsActive)
        {
            return Unauthorized(new
            {
                message =
                    "Your branch is inactive. Please contact an administrator."
            });
        }

        // Extra safety:
        // Make sure the branch actually belongs to the user's tenant.

        if (user.Branch.TenantId != user.TenantId)
        {
            return Unauthorized(new
            {
                message =
                    "Your account has an invalid branch configuration."
            });
        }


        // ============================================================
        // GENERATE JWT
        // ============================================================

        var accessToken =
            _jwtGenerator.GenerateToken(user);

        var refreshToken =
            _jwtGenerator.GenerateRefreshToken();


        // ============================================================
        // ROLE
        // ============================================================

        var role =
            user.Role switch
            {
                Estuscia.Domain.Enums.UserRole.SuperAdmin
                    => "super_admin",

                Estuscia.Domain.Enums.UserRole.CompanyAdmin
                    => "company_admin",

                Estuscia.Domain.Enums.UserRole.HrOps
                    => "hr_ops",

                Estuscia.Domain.Enums.UserRole.BranchManager
                    => "branch_manager",

                Estuscia.Domain.Enums.UserRole.SalesStaff
                    => "sales_staff",

                Estuscia.Domain.Enums.UserRole.Developer
                    => "developer",

                Estuscia.Domain.Enums.UserRole.SupportStaff
                    => "support_staff",

                Estuscia.Domain.Enums.UserRole.KnowledgeTrainer
                    => "knowledge_trainer",

                _ => throw new ArgumentOutOfRangeException(
                    nameof(user.Role),
                    user.Role,
                    "Unsupported user role.")
            };


        // ============================================================
        // RESPONSE
        // ============================================================

        var response =
            new AuthResponseDto(
                accessToken,
                refreshToken,
                new UserDto(
                    user.Id,
                    user.Email,
                    user.FullName,
                    role,
                    user.Designation ?? string.Empty,
                    user.BranchId,
                    user.Branch.BranchName,
                    user.TenantId,
                    user.Tenant.Name,
                    user.AvatarUrl ?? string.Empty
                )
            );

        return Ok(response);
    }

    
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userIdClaim =
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst("user_id")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await _context.Users
    .Include(u => u.Tenant)
    .Include(u => u.Branch)
    .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return Unauthorized();
        }

        if (!user.IsActive)
        {
            return Unauthorized(new
            {
                message = "Your account is inactive."
            });
        }

        if (user.Tenant == null)
        {
            return Unauthorized(new
            {
                message = "Your account is not associated with an organization."
            });
        }

        if (!user.Tenant.IsActive)
        {
            return Unauthorized(new
            {
                message = "This organization is inactive."
            });
        }

        var role =
            user.Role switch
            {
                Estuscia.Domain.Enums.UserRole.SuperAdmin
                    => "super_admin",

                Estuscia.Domain.Enums.UserRole.CompanyAdmin
                    => "company_admin",

                Estuscia.Domain.Enums.UserRole.HrOps
                    => "hr_ops",

                Estuscia.Domain.Enums.UserRole.BranchManager
                    => "branch_manager",

                Estuscia.Domain.Enums.UserRole.SalesStaff
                    => "sales_staff",

                Estuscia.Domain.Enums.UserRole.Developer
                    => "developer",

                Estuscia.Domain.Enums.UserRole.SupportStaff
                    => "support_staff",

                Estuscia.Domain.Enums.UserRole.KnowledgeTrainer
                    => "knowledge_trainer",

                _ => throw new ArgumentOutOfRangeException(
                    nameof(user.Role),
                    user.Role,
                    "Unsupported user role.")
            };

        var response = new UserDto(
    user.Id,
    user.Email,
    user.FullName,
    role,
    user.Designation ?? string.Empty,
    user.BranchId,
    user.Branch.BranchName,
    user.TenantId,
    user.Tenant.Name,
    user.AvatarUrl ?? string.Empty
);

        return Ok(response);
    }
}