using Estuscia.Application.Common.DTOs;
using Estuscia.Application.Common.Interfaces;
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

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Email and password are required."
            });
        }

        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u =>
                u.Email.ToLower() == email);

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new
            {
                message = "Your account is inactive. Please contact an administrator."
            });
        }

        if (!BCrypt.Net.BCrypt.Verify(
                request.Password,
                user.PasswordHash))
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        if (!user.Tenant.IsActive)
        {
            return Unauthorized(new
            {
                message = "This organization is inactive."
            });
        }

        var accessToken = _jwtGenerator.GenerateToken(user);
        var refreshToken = _jwtGenerator.GenerateRefreshToken();

        var response = new AuthResponseDto(
            accessToken,
            refreshToken,
            new UserDto(
                user.Id,
                user.Email,
                user.FullName,
                user.Role.ToString().ToLowerInvariant(),
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