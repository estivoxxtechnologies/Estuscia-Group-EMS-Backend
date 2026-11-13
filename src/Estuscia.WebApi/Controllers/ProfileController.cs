
using System.Security.Claims;
using Estuscia.Application.Common.Interfaces;
using Estuscia.Application.Users.DTOs.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Estuscia.WebApi.Security;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IAppDbContext _context;
    private readonly IFileStorageService _fileStorage;

    public ProfileController(
        IAppDbContext context,
        IFileStorageService fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    // ============================================================
    // CURRENT USER ID
    // ============================================================

    private int? GetCurrentUserId()
    {
        var userIdClaim =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst("user_id")?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return null;
        }

        return userId;
    }

    // ============================================================
    // GET AVATAR DISPLAY URL
    // ============================================================

    private string? GetAvatarDisplayUrl(string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
            return null;

        const string prefix = "/uploads/avatars/";

        var path = avatarUrl;

        if (Uri.TryCreate(
            avatarUrl,
            UriKind.Absolute,
            out var uri))
        {
            path = uri.AbsolutePath;
        }

        if (!path.StartsWith(
            prefix,
            StringComparison.OrdinalIgnoreCase))
        {
            return avatarUrl;
        }

        var fileName = Path.GetFileName(path);

        if (string.IsNullOrWhiteSpace(fileName))
            return avatarUrl;

        var key = $"uploads/avatars/{fileName}";

        return _fileStorage.GetPresignedUrl(key);
    }

    // ============================================================
    // GET MY PROFILE
    // GET /api/Profile/me
    // ============================================================

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new
            {
                message = "Invalid user identity."
            });
        }

        var user = await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .Include(u => u.Branch)
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null)
        {
            return NotFound(new
            {
                message = "User profile not found."
            });
        }

        var response = new MyProfileDto
        {
            Id = user.Id,

            // Personal
            FullName = user.FullName,
            Email = user.Email,
            AvatarUrl = GetAvatarDisplayUrl(user.AvatarUrl) ?? string.Empty,

            // Employee
            EmployeeCode = user.EmployeeCode,

            RoleName = user.Role?.DisplayName
                       ?? user.Role?.RoleName
                       ?? string.Empty,

            Designation = user.Designation,
            Department = user.Department,

            // Organization
            TenantId = user.TenantId,
            TenantName = user.Tenant?.Name ?? string.Empty,

            BranchId = user.BranchId,
            BranchName = user.Branch?.BranchName,

            // Account
            IsActive = user.IsActive
        };

        return Ok(response);
    }

    // ============================================================
    // UPDATE MY PROFILE
    // PUT /api/Profile/me
    // ============================================================

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile(
        [FromBody] UpdateMyProfileDto request)
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new
            {
                message = "Invalid user identity."
            });
        }

        // --------------------------------------------------------
        // VALIDATION
        // --------------------------------------------------------

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

        var email = request.Email
            .Trim()
            .ToLowerInvariant();

        // --------------------------------------------------------
        // LOAD CURRENT USER
        // --------------------------------------------------------

        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null)
        {
            return NotFound(new
            {
                message = "User profile not found."
            });
        }

        // --------------------------------------------------------
        // EMAIL DUPLICATE CHECK
        // --------------------------------------------------------

        var emailExists = await _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u =>
                u.Id != user.Id &&
                u.TenantId == user.TenantId &&
                u.Email.ToLower() == email);

        if (emailExists)
        {
            return Conflict(new
            {
                message = "This email address is already used by another user."
            });
        }

        // --------------------------------------------------------
        // UPDATE ONLY SELF-EDITABLE FIELDS
        // --------------------------------------------------------

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Profile updated successfully."
        });
    }

    // ============================================================
    // CHANGE PASSWORD
    // PUT /api/Profile/me/password
    // ============================================================

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordDto request)
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new
            {
                message = "Invalid user identity."
            });
        }

        // --------------------------------------------------------
        // VALIDATION
        // --------------------------------------------------------

        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            return BadRequest(new
            {
                message = "Current password is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new
            {
                message = "New password is required."
            });
        }

        if (request.NewPassword.Length < 8)
        {
            return BadRequest(new
            {
                message = "New password must contain at least 8 characters."
            });
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            return BadRequest(new
            {
                message = "New password and confirmation password do not match."
            });
        }

        // --------------------------------------------------------
        // LOAD USER
        // --------------------------------------------------------

        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null)
        {
            return NotFound(new
            {
                message = "User account not found."
            });
        }

        // --------------------------------------------------------
        // VERIFY CURRENT PASSWORD
        // --------------------------------------------------------

        var currentPasswordValid =
            BCrypt.Net.BCrypt.Verify(
                request.CurrentPassword,
                user.PasswordHash);

        if (!currentPasswordValid)
        {
            return BadRequest(new
            {
                message = "Current password is incorrect."
            });
        }

        // --------------------------------------------------------
        // PREVENT SAME PASSWORD
        // --------------------------------------------------------

        if (BCrypt.Net.BCrypt.Verify(
                request.NewPassword,
                user.PasswordHash))
        {
            return BadRequest(new
            {
                message = "New password must be different from your current password."
            });
        }

        // --------------------------------------------------------
        // HASH NEW PASSWORD
        // --------------------------------------------------------

        user.PasswordHash =
            BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        user.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Password changed successfully."
        });
    }

    // ============================================================
    // UPLOAD PROFILE AVATAR
    // POST /api/Profile/me/avatar
    // ============================================================

    [HttpPost("me/avatar")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadAvatar(
        IFormFile file)
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new
            {
                message = "Invalid user identity."
            });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new
            {
                message = "Please select a profile image."
            });
        }

        // --------------------------------------------------------
        // FILE SIZE
        // --------------------------------------------------------

        const long maxFileSize = 5 * 1024 * 1024;

        if (file.Length > maxFileSize)
        {
            return BadRequest(new
            {
                message = "Profile image must be smaller than 5 MB."
            });
        }

        // --------------------------------------------------------
        // EXTENSION
        // --------------------------------------------------------

        var extension =
            Path.GetExtension(file.FileName)
                .ToLowerInvariant();

        var allowedExtensions = new[]
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new
            {
                message = "Only JPG, JPEG, PNG and WEBP images are allowed."
            });
        }

        if (!await UploadedFileSignatureValidator.IsValidAsync(
        file, extension, HttpContext.RequestAborted))
        {
            return BadRequest(new
            {
                message = "The uploaded file is not a valid image."
            });
        }
        // --------------------------------------------------------
        // LOAD USER
        // --------------------------------------------------------

        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null)
        {
            return NotFound(new
            {
                message = "User account not found."
            });
        }

        // --------------------------------------------------------
        // GENERATE S3 KEY
        // --------------------------------------------------------

        var fileName =
            $"{user.Id}_{Guid.NewGuid():N}{extension}";

        var storageKey =
            $"uploads/avatars/{fileName}";

        // --------------------------------------------------------
        // UPLOAD TO S3
        // --------------------------------------------------------

        var contentType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => throw new InvalidOperationException("Unsupported image format.")
        };

        if (string.IsNullOrWhiteSpace(contentType))
        {
            contentType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }

        await using (var stream = file.OpenReadStream())
        {
            await _fileStorage.UploadAsync(
                storageKey,
                stream,
                contentType);
        }

        // --------------------------------------------------------
        // SAVE DATABASE URL
        // --------------------------------------------------------

        var previousAvatarUrl = user.AvatarUrl;

        user.AvatarUrl =
            $"/uploads/avatars/{fileName}";

        user.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            // Remove newly uploaded S3 file if DB save fails.
            try
            {
                await _fileStorage.DeleteAsync(storageKey);
            }
            catch
            {
                // Preserve original database error.
            }

            throw;
        }

        // --------------------------------------------------------
        // DELETE OLD AVATAR FROM S3
        // --------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(previousAvatarUrl))
        {
            try
            {
                var oldPath = previousAvatarUrl;

                if (Uri.TryCreate(
                    previousAvatarUrl,
                    UriKind.Absolute,
                    out var oldUri))
                {
                    oldPath = oldUri.AbsolutePath;
                }

                const string prefix = "/uploads/avatars/";

                if (oldPath.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
                {
                    var oldFileName = Path.GetFileName(oldPath);

                    if (!string.IsNullOrWhiteSpace(oldFileName) &&
                        oldFileName != fileName)
                    {
                        await _fileStorage.DeleteAsync(
                            $"uploads/avatars/{oldFileName}");
                    }
                }
            }
            catch
            {
                // Do not fail the successful upload because
                // old-file cleanup failed.
            }
        }

        // --------------------------------------------------------
        // RETURN TEMPORARY S3 URL
        // --------------------------------------------------------

        return Ok(new
        {
            message = "Profile photo updated successfully.",
            avatarUrl = _fileStorage.GetPresignedUrl(storageKey)
        });
    }
}
