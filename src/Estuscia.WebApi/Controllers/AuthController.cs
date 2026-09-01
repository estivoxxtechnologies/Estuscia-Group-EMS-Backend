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
        // --------------------------------------------------------
        // Validate request
        // --------------------------------------------------------

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Email and password are required."
            });
        }

        var email =
            request.Email
                .Trim()
                .ToLowerInvariant();

        // --------------------------------------------------------
        // Find user
        //
        // IMPORTANT:
        // Login is intentionally allowed to bypass the normal
        // tenant query filter because we don't know the tenant
        // until we identify the user.
        // --------------------------------------------------------

        var user =
            await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(
                    u => u.Email.ToLower() == email);

        // --------------------------------------------------------
        // Do not reveal whether the email exists.
        // --------------------------------------------------------

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        // --------------------------------------------------------
        // Account status
        // --------------------------------------------------------

        if (!user.IsActive)
        {
            return Unauthorized(new
            {
                message =
                    "Your account is inactive. Please contact an administrator."
            });
        }

        // --------------------------------------------------------
        // Password verification
        // --------------------------------------------------------

        if (!BCrypt.Net.BCrypt.Verify(
                request.Password,
                user.PasswordHash))
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        // --------------------------------------------------------
        // Tenant validation
        // --------------------------------------------------------

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

        // --------------------------------------------------------
        // Generate JWT
        // --------------------------------------------------------

        var accessToken =
            _jwtGenerator.GenerateToken(user);

        var refreshToken =
            _jwtGenerator.GenerateRefreshToken();

        // --------------------------------------------------------
        // Return authenticated user
        //
        // IMPORTANT:
        // Keep the role naming consistent with the JWT.
        // --------------------------------------------------------

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

        var response =
            new AuthResponseDto(
                accessToken,
                refreshToken,
                new UserDto(
                    user.Id,
                    user.Email,
                    user.FullName,
                    role,
                    user.Designation,
                    user.BranchName,
                    user.TenantId,
                    user.Tenant.Name,
                    user.AvatarUrl
                )
            );

        return Ok(response);
    }
}