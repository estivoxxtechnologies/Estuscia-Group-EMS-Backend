
using System.Security.Claims;
using Estuscia.Domain.Entities;
using Estuscia.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class UserDetailsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<UserDetailsController> _logger;

    private const long MaxFileSize = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedDocumentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "EducationCertificate",
            "Aadhaar",
            "PAN",
            "BankDocument",
            "PassportPhoto",
            "Passport",
            "Other"
        };

    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "image/jpeg",
            "image/png"
        };

    private static readonly HashSet<string> ManagementRoles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "super_admin",
            "company_admin",
            "hr_ops",
            "branch_manager"
        };

    public UserDetailsController(
        AppDbContext db,
        IWebHostEnvironment environment,
        ILogger<UserDetailsController> logger)
    {
        _db = db;
        _environment = environment;
        _logger = logger;
    }

    // ----------------------------------------------------
    // GET MY PROFILE
    // GET: api/UserDetails/me
    // ----------------------------------------------------

    [HttpGet("me")]
    public async Task<IActionResult> GetMyDetails(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new
            {
                message = "The authenticated user ID could not be identified."
            });
        }

        var user = await GetAuthorizedEmployeeAsync(
            userId.Value,
            cancellationToken);

        if (user is null)
            return NotFound(new { message = "Employee not found." });

        var details = await _db.UserDetails
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == userId.Value,
                cancellationToken);

        var documents = await _db.UserDocuments
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .OrderByDescending(x => x.UploadedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.DocumentType,
                x.OriginalFileName,
                x.ContentType,
                x.FileSizeBytes,
                x.VerificationStatus,
                x.VerifiedAtUtc,
                x.RejectionReason,
                x.UploadedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            employee = new
            {
                user.Id,
                user.FullName,
                user.EmployeeCode,
                user.Email,
                user.Designation,
                user.Department,
                user.TenantId,
                user.BranchId
            },

            details = details is null ? null : new
            {
                details.Id,
                details.UserId,
                details.DateOfBirth,
                details.DateOfJoining,
                details.PhoneNumber,
                details.OfficialNumber,
                details.AddressLine1,
                details.AddressLine2,
                details.City,
                details.State,
                details.PostalCode,
                details.Country,
                details.EmergencyContactName,
                details.EmergencyContactNumber,
                details.BankName,
                details.AccountHolderName,
                details.AccountNumber,
                details.IFSCCode,
                details.BankBranchName,
                details.Notes
            },

            documents
        });
    }

    // ----------------------------------------------------
    // UPDATE MY PROFILE
    // PUT: api/UserDetails/me
    // ----------------------------------------------------

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyDetails(
        [FromBody] UpdateUserDetailsRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var user = await GetAuthorizedEmployeeAsync(
            userId.Value,
            cancellationToken);

        if (user is null)
            return NotFound(new { message = "Employee not found." });

        if (!IsValidRequest(request, out var validationMessage))
        {
            return BadRequest(new { message = validationMessage });
        }

        var details = await _db.UserDetails
            .FirstOrDefaultAsync(
                x => x.UserId == userId.Value,
                cancellationToken);

        if (details is null)
        {
            details = new UserDetails
            {
                UserId = user.Id,
                TenantId = user.TenantId,
                BranchId = user.BranchId
            };

            _db.UserDetails.Add(details);
        }

        ApplyProfileChanges(details, request);

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Your employee details have been updated successfully.",

            details = new
            {
                details.Id,
                details.UserId,
                details.DateOfBirth,
                details.DateOfJoining,
                details.PhoneNumber,
                details.OfficialNumber,
                details.AddressLine1,
                details.AddressLine2,
                details.City,
                details.State,
                details.PostalCode,
                details.Country,
                details.EmergencyContactName,
                details.EmergencyContactNumber,
                details.BankName,
                details.AccountHolderName,
                details.AccountNumber,
                details.IFSCCode,
                details.BankBranchName,
                details.Notes
            }
        });
    }

    // ----------------------------------------------------
    // GET AN EMPLOYEE'S PROFILE
    // GET: api/UserDetails/employees/{userId}
    // Management access
    // ----------------------------------------------------

    [HttpGet("employees/{userId:int}")]
    public async Task<IActionResult> GetEmployeeDetails(
        int userId,
        CancellationToken cancellationToken)
    {
        if (!IsManagement())
            return Forbid();

        var employee = await GetAuthorizedEmployeeAsync(
            userId,
            cancellationToken);

        if (employee is null)
        {
            return NotFound(new
            {
                message = "Employee not found in your permitted scope."
            });
        }

        var details = await _db.UserDetails
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == userId,
                cancellationToken);

        var documents = await _db.UserDocuments
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UploadedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.DocumentType,
                x.OriginalFileName,
                x.ContentType,
                x.FileSizeBytes,
                x.VerificationStatus,
                x.VerifiedAtUtc,
                x.RejectionReason,
                x.UploadedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            employee = new
            {
                employee.Id,
                employee.FullName,
                employee.EmployeeCode,
                employee.Email,
                employee.Designation,
                employee.Department,
                employee.TenantId,
                employee.BranchId
            },

            details = details is null ? null : new
            {
                details.Id,
                details.UserId,
                details.DateOfBirth,
                details.DateOfJoining,
                details.PhoneNumber,
                details.OfficialNumber,
                details.AddressLine1,
                details.AddressLine2,
                details.City,
                details.State,
                details.PostalCode,
                details.Country,
                details.EmergencyContactName,
                details.EmergencyContactNumber,
                details.BankName,
                details.AccountHolderName,
                details.AccountNumber,
                details.IFSCCode,
                details.BankBranchName,
                details.Notes
            },

            documents
        });
    }

    // ----------------------------------------------------
    // UPDATE AN EMPLOYEE'S PROFILE
    // PUT: api/UserDetails/employees/{userId}
    // Management access
    // ----------------------------------------------------

    [HttpPut("employees/{userId:int}")]
    public async Task<IActionResult> UpdateEmployeeDetails(
        int userId,
        [FromBody] UpdateEmployeeDetailsRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsManagement())
            return Forbid();

        var employee = await GetAuthorizedEmployeeAsync(
            userId,
            cancellationToken);

        if (employee is null)
        {
            return NotFound(new
            {
                message = "Employee not found in your permitted scope."
            });
        }

        if (!IsValidRequest(request, out var validationMessage))
        {
            return BadRequest(new { message = validationMessage });
        }

        var details = await _db.UserDetails
            .FirstOrDefaultAsync(
                x => x.UserId == userId,
                cancellationToken);

        if (details is null)
        {
            details = new UserDetails
            {
                UserId = employee.Id,
                TenantId = employee.TenantId,
                BranchId = employee.BranchId
            };

            _db.UserDetails.Add(details);
        }

        ApplyProfileChanges(details, request);

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Employee details updated successfully.",
            employeeId = employee.Id,
            detailsId = details.Id
        });
    }

    // ----------------------------------------------------
    // UPLOAD AN EMPLOYEE DOCUMENT
    // POST: api/UserDetails/employees/{userId}/documents
    // ----------------------------------------------------

    [HttpPost("employees/{userId:int}/documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<IActionResult> UploadEmployeeDocument(
        int userId,
        IFormFile? file,
        [FromForm] string documentType,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        if (currentUserId is null)
            return Unauthorized();

        var isSelf = currentUserId.Value == userId;

        if (!isSelf && !IsManagement())
            return Forbid();

        if (file is null || file.Length == 0)
        {
            return BadRequest(new
            {
                message = "Please select a file to upload."
            });
        }

        if (file.Length > MaxFileSize)
        {
            return BadRequest(new
            {
                message = "The maximum allowed file size is 10 MB."
            });
        }

        if (string.IsNullOrWhiteSpace(documentType) ||
            !AllowedDocumentTypes.Contains(documentType.Trim()))
        {
            return BadRequest(new
            {
                message = "The supplied document type is not supported."
            });
        }

        var contentType = file.ContentType?.Trim() ?? string.Empty;

        if (!AllowedContentTypes.Contains(contentType))
        {
            return BadRequest(new
            {
                message = "Only PDF, JPEG and PNG documents are allowed."
            });
        }

        var employee = await GetAuthorizedEmployeeAsync(
            userId,
            cancellationToken);

        if (employee is null)
        {
            return NotFound(new
            {
                message = "Employee not found in your permitted scope."
            });
        }

        var originalFileName = Path.GetFileName(file.FileName);

        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return BadRequest(new
            {
                message = "The supplied filename is invalid."
            });
        }

        var extension = Path.GetExtension(originalFileName)
            .ToLowerInvariant();

        var validExtension = contentType switch
        {
            "application/pdf" => extension == ".pdf",
            "image/jpeg" => extension is ".jpg" or ".jpeg",
            "image/png" => extension == ".png",
            _ => false
        };

        if (!validExtension)
        {
            return BadRequest(new
            {
                message = "The file extension does not match its content type."
            });
        }

        // Store documents outside wwwroot.
        var storageDirectory = Path.Combine(
            _environment.ContentRootPath,
            "PrivateEmployeeDocuments");

        Directory.CreateDirectory(storageDirectory);

        var storageKey = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(storageDirectory, storageKey);

        try
        {
            await using (var stream = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var document = new UserDocument
            {
                UserId = employee.Id,
                TenantId = employee.TenantId,
                BranchId = employee.BranchId,
                DocumentType = documentType.Trim(),
                OriginalFileName = originalFileName,
                ContentType = contentType,
                FileSizeBytes = file.Length,
                StorageProvider = "LocalPrivate",
                StorageKey = storageKey,
                VerificationStatus = "Pending",
                UploadedByUserId = currentUserId.Value,
                UploadedAtUtc = DateTime.UtcNow
            };

            _db.UserDocuments.Add(document);

            await _db.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(
                nameof(DownloadDocument),
                new { documentId = document.Id },
                new
                {
                    message = "Document uploaded successfully and is pending review.",
                    document = new
                    {
                        document.Id,
                        document.UserId,
                        document.DocumentType,
                        document.OriginalFileName,
                        document.ContentType,
                        document.FileSizeBytes,
                        document.VerificationStatus,
                        document.UploadedAtUtc
                    }
                });
        }
        catch (Exception ex)
        {
            try
            {
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }
            catch (IOException cleanupException)
            {
                _logger.LogError(
                    cleanupException,
                    "Failed to clean up an incomplete employee document upload.");
            }

            _logger.LogError(
                ex,
                "Employee document upload failed for employee {UserId}.",
                userId);

            throw;
        }
    }

    // ----------------------------------------------------
    // DOWNLOAD A DOCUMENT
    // GET: api/UserDetails/documents/{documentId}/download
    // ----------------------------------------------------

    [HttpGet("documents/{documentId:int}/download")]
    public async Task<IActionResult> DownloadDocument(
        int documentId,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        if (currentUserId is null)
            return Unauthorized();

        var document = await _db.UserDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == documentId,
                cancellationToken);

        if (document is null)
            return NotFound(new { message = "Document not found." });

        var isSelf = document.UserId == currentUserId.Value;

        if (!isSelf && !IsManagement())
            return Forbid();

        var employee = await GetAuthorizedEmployeeAsync(
            document.UserId,
            cancellationToken);

        if (employee is null)
        {
            return NotFound(new
            {
                message = "Document not found in your permitted scope."
            });
        }

        if (string.IsNullOrWhiteSpace(document.StorageKey) ||
            document.StorageKey != Path.GetFileName(document.StorageKey))
        {
            _logger.LogWarning(
                "Invalid storage key found for document {DocumentId}.",
                documentId);

            return NotFound(new { message = "Document file not found." });
        }

        var storageDirectory = Path.GetFullPath(Path.Combine(
            _environment.ContentRootPath,
            "PrivateEmployeeDocuments"));

        var filePath = Path.GetFullPath(Path.Combine(
            storageDirectory,
            document.StorageKey));

        if (!filePath.StartsWith(
                storageDirectory + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (!System.IO.File.Exists(filePath))
            return NotFound(new { message = "Document file not found." });

        var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return File(
            stream,
            document.ContentType,
            document.OriginalFileName,
            enableRangeProcessing: false);
    }

    // ----------------------------------------------------
    // REVIEW A DOCUMENT
    // PUT: api/UserDetails/documents/{documentId}/review
    // ----------------------------------------------------

    [HttpPut("documents/{documentId:int}/review")]
    public async Task<IActionResult> ReviewDocument(
        int documentId,
        [FromBody] ReviewDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsManagement())
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.VerificationStatus))
        {
            return BadRequest(new
            {
                message = "A verification status is required."
            });
        }

        var verificationStatus = request.VerificationStatus.Trim();

        if (!verificationStatus.Equals(
                "Verified",
                StringComparison.OrdinalIgnoreCase) &&
            !verificationStatus.Equals(
                "Rejected",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Verification status must be Verified or Rejected."
            });
        }

        if (verificationStatus.Equals(
                "Rejected",
                StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(request.RejectionReason))
        {
            return BadRequest(new
            {
                message = "A rejection reason is required when rejecting a document."
            });
        }

        var reviewerId = GetCurrentUserId();

        if (reviewerId is null)
            return Unauthorized();

        var document = await _db.UserDocuments
            .FirstOrDefaultAsync(
                x => x.Id == documentId,
                cancellationToken);

        if (document is null)
            return NotFound(new { message = "Document not found." });

        var employee = await GetAuthorizedEmployeeAsync(
            document.UserId,
            cancellationToken);

        if (employee is null)
        {
            return NotFound(new
            {
                message = "Document not found in your permitted scope."
            });
        }

        document.VerificationStatus =
            verificationStatus.Equals(
                "Verified",
                StringComparison.OrdinalIgnoreCase)
                ? "Verified"
                : "Rejected";

        document.VerifiedByUserId = reviewerId.Value;
        document.VerifiedAtUtc = DateTime.UtcNow;
        document.RejectionReason =
            document.VerificationStatus == "Rejected"
                ? request.RejectionReason!.Trim()
                : null;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = document.VerificationStatus == "Verified"
                ? "Document verified successfully."
                : "Document rejected. The employee can review the reason and upload a corrected document.",

            document = new
            {
                document.Id,
                document.UserId,
                document.DocumentType,
                document.OriginalFileName,
                document.VerificationStatus,
                document.VerifiedByUserId,
                document.VerifiedAtUtc,
                document.RejectionReason
            }
        });
    }

    // ----------------------------------------------------
    // AUTHORIZED EMPLOYEE LOOKUP
    // ----------------------------------------------------

    private async Task<ApplicationUser?> GetAuthorizedEmployeeAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var role = GetCurrentRole();

        if (string.IsNullOrWhiteSpace(role))
            return null;

        var query = _db.Users.AsNoTracking()
            .Where(x => x.Id == userId);

        var currentTenantId = GetCurrentTenantId();

        if (role == "super_admin")
        {
            if (Request.Headers.TryGetValue(
                    "X-Tenant-Id",
                    out var tenantHeader))
            {
                if (!int.TryParse(
                        tenantHeader.ToString(),
                        out var selectedTenantId) ||
                    selectedTenantId <= 0)
                {
                    return null;
                }

                query = query.Where(
                    x => x.TenantId == selectedTenantId);
            }
        }
        else
        {
            if (currentTenantId is null || currentTenantId <= 0)
                return null;

            query = query.Where(
                x => x.TenantId == currentTenantId.Value);
        }

        if (role == "branch_manager")
        {
            var branchId = GetCurrentBranchId();

            if (branchId is null || branchId <= 0)
                return null;

            query = query.Where(
                x => x.BranchId == branchId.Value);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    // ----------------------------------------------------
    // PROFILE VALIDATION AND MAPPING
    // ----------------------------------------------------

    private static bool IsValidRequest(
        UpdateUserDetailsRequest request,
        out string message)
    {
        if (request.DateOfBirth.HasValue &&
            request.DateOfBirth.Value.Date > DateTime.UtcNow.Date)
        {
            message = "Date of birth cannot be in the future.";
            return false;
        }

        if (request.DateOfJoining.HasValue &&
            request.DateOfJoining.Value.Date > DateTime.UtcNow.Date)
        {
            message = "Date of joining cannot be in the future.";
            return false;
        }

        if (request.DateOfBirth.HasValue &&
            request.DateOfJoining.HasValue &&
            request.DateOfJoining.Value.Date <
            request.DateOfBirth.Value.Date)
        {
            message = "Date of joining cannot be earlier than date of birth.";
            return false;
        }

        if (!IsWithinLength(request.PhoneNumber, 30) ||
            !IsWithinLength(request.OfficialNumber, 30) ||
            !IsWithinLength(request.AddressLine1, 200) ||
            !IsWithinLength(request.AddressLine2, 200) ||
            !IsWithinLength(request.City, 100) ||
            !IsWithinLength(request.State, 100) ||
            !IsWithinLength(request.PostalCode, 20) ||
            !IsWithinLength(request.Country, 100) ||
            !IsWithinLength(request.EmergencyContactName, 150) ||
            !IsWithinLength(request.EmergencyContactNumber, 30) ||
            !IsWithinLength(request.BankName, 150) ||
            !IsWithinLength(request.AccountHolderName, 150) ||
            !IsWithinLength(request.AccountNumber, 34) ||
            !IsWithinLength(request.IFSCCode, 20) ||
            !IsWithinLength(request.BankBranchName, 150) ||
            !IsWithinLength(request.Notes, 2000))
        {
            message = "One or more fields exceed their allowed length.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool IsWithinLength(
        string? value,
        int maxLength)
    {
        return value is null || value.Length <= maxLength;
    }

    private static void ApplyProfileChanges(
        UserDetails details,
        UpdateUserDetailsRequest request)
    {
        details.DateOfBirth = request.DateOfBirth;
        details.DateOfJoining = request.DateOfJoining;

        details.PhoneNumber = Clean(request.PhoneNumber);
        details.OfficialNumber = Clean(request.OfficialNumber);

        details.AddressLine1 = Clean(request.AddressLine1);
        details.AddressLine2 = Clean(request.AddressLine2);
        details.City = Clean(request.City);
        details.State = Clean(request.State);
        details.PostalCode = Clean(request.PostalCode);
        details.Country = Clean(request.Country);

        details.EmergencyContactName =
            Clean(request.EmergencyContactName);

        details.EmergencyContactNumber =
            Clean(request.EmergencyContactNumber);

        details.BankName = Clean(request.BankName);
        details.AccountHolderName = Clean(request.AccountHolderName);

        // Store the account number as a normal string.
        // An empty account-number field preserves the existing value.
        if (!string.IsNullOrWhiteSpace(request.AccountNumber))
        {
            details.AccountNumber = request.AccountNumber.Trim();
        }

        details.IFSCCode = Clean(request.IFSCCode);
        details.BankBranchName = Clean(request.BankBranchName);
        details.Notes = Clean(request.Notes);
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    // ----------------------------------------------------
    // CLAIM HELPERS
    // ----------------------------------------------------

    private int? GetCurrentUserId()
    {
        return GetClaimInt(
            ClaimTypes.NameIdentifier,
            "userId",
            "UserId",
            "sub");
    }

    private int? GetCurrentTenantId()
    {
        return GetClaimInt(
            "tenantId",
            "TenantId",
            "tenant_id");
    }

    private int? GetCurrentBranchId()
    {
        return GetClaimInt(
            "branchId",
            "BranchId",
            "branch_id");
    }

    private string? GetCurrentRole()
    {
        var role = User.FindFirstValue(ClaimTypes.Role)
            ?? User.FindFirstValue("role")
            ?? User.FindFirstValue("RoleName")
            ?? User.FindFirstValue("roleName");

        return string.IsNullOrWhiteSpace(role)
            ? null
            : role.Trim().ToLowerInvariant();
    }

    private int? GetClaimInt(params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = User.FindFirstValue(claimType);

            if (int.TryParse(value, out var parsed))
                return parsed;
        }

        return null;
    }

    private bool IsManagement()
    {
        var role = GetCurrentRole();

        return role is not null &&
               ManagementRoles.Contains(role);
    }
}

// ========================================================
// REQUEST DTOs
// ========================================================

public class UpdateUserDetailsRequest
{
    public DateTime? DateOfBirth { get; set; }
    public DateTime? DateOfJoining { get; set; }

    public string? PhoneNumber { get; set; }
    public string? OfficialNumber { get; set; }

    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactNumber { get; set; }

    public string? BankName { get; set; }
    public string? AccountHolderName { get; set; }
    public string? AccountNumber { get; set; }
    public string? IFSCCode { get; set; }
    public string? BankBranchName { get; set; }

    public string? Notes { get; set; }
}

public sealed class UpdateEmployeeDetailsRequest
    : UpdateUserDetailsRequest
{
}

public sealed class ReviewDocumentRequest
{
    public string VerificationStatus { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
}
