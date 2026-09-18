using Estuscia.Application.Common.DTOs;
using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Entities;
using Estuscia.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SalesLeadsController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly ICurrentTenantService _tenantService;

    public SalesLeadsController(
        IAppDbContext db,
        ICurrentTenantService tenantService)
    {
        _db = db;
        _tenantService = tenantService;
    }


    // ============================================================
    // CURRENT USER
    // ============================================================

    private async Task<ApplicationUser?> GetCurrentUserAsync(
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return null;

        return await _db.Users
            .IgnoreQueryFilters()
            .Include(x => x.Role)
            .FirstOrDefaultAsync(
                x => x.Id == _tenantService.UserId.Value,
                cancellationToken);
    }


    // ============================================================
    // ROLE HELPERS
    // ============================================================

    private static bool IsManagementRole(ApplicationUser user)
    {
        return user.RoleNumber == 2 || // company_admin
               user.RoleNumber == 3 || // hr_ops
               user.RoleNumber == 4;   // branch_manager
    }


    private static bool IsSalesStaff(ApplicationUser user)
    {
        return user.RoleNumber == 5;
    }


    private static bool IsSeniorSales(ApplicationUser user)
    {
        if (!IsSalesStaff(user))
            return false;

        return !string.IsNullOrWhiteSpace(user.Designation) &&
               user.Designation.Trim()
                   .Equals(
                       "senior",
                       StringComparison.OrdinalIgnoreCase);
    }


    private static bool CanAssignLeads(ApplicationUser user)
    {
        if (user.RoleNumber == 2 ||
            user.RoleNumber == 3 ||
            user.RoleNumber == 4)
        {
            return true;
        }

        return IsSeniorSales(user);
    }


    private static bool CanViewTeam(ApplicationUser user)
    {
        return user.RoleNumber == 2 ||
               user.RoleNumber == 3 ||
               user.RoleNumber == 4 ||
               IsSeniorSales(user);
    }


    // ============================================================
    // BRANCH ACCESS
    // ============================================================

    private static bool CanAccessBranch(
        ApplicationUser currentUser,
        int branchId)
    {
        // Company Admin / HR Ops can work across tenant branches.
        if (currentUser.RoleNumber == 2 ||
            currentUser.RoleNumber == 3)
        {
            return true;
        }

        // Branch Manager and Sales Staff are branch scoped.
        return currentUser.BranchId == branchId;
    }


    // ============================================================
    // GET ALL LEADS
    //
    // Management/team use.
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetLeads(
        [FromQuery] int? branchId,
        [FromQuery] int? assignedToUserId,
        [FromQuery] SalesLeadOutcome? outcome,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        var currentUser =
            await GetCurrentUserAsync(cancellationToken);

        if (currentUser == null)
            return Unauthorized();


        if (!CanViewTeam(currentUser))
        {
            return Forbid();
        }


        var query = _db.SalesLeadAssignments
            .AsNoTracking()
            .Include(x => x.SalesLead)
            .Include(x => x.Branch)
            .Include(x => x.AssignedToUser)
            .Include(x => x.AssignedByUser)
            .AsQueryable();


        // --------------------------------------------------------
        // Branch restriction
        // --------------------------------------------------------

        if (branchId.HasValue)
        {
            if (!CanAccessBranch(
                    currentUser,
                    branchId.Value))
            {
                return Forbid();
            }

            query = query.Where(
                x => x.BranchId == branchId.Value);
        }
        else
        {
            // Branch manager / senior sales can only see own branch.
            if (currentUser.RoleNumber == 4 ||
                IsSeniorSales(currentUser))
            {
                if (!currentUser.BranchId.HasValue)
                    return BadRequest(new
                    {
                        message =
                            "Your account is not associated with a branch."
                    });

                query = query.Where(
                    x => x.BranchId ==
                         currentUser.BranchId.Value);
            }
        }


        // --------------------------------------------------------
        // Employee filter
        // --------------------------------------------------------

        if (assignedToUserId.HasValue)
        {
            query = query.Where(
                x => x.AssignedToUserId ==
                     assignedToUserId.Value);
        }


        // --------------------------------------------------------
        // Outcome filter
        // --------------------------------------------------------

        if (outcome.HasValue)
        {
            query = query.Where(
                x => x.Outcome == outcome.Value);
        }


        // --------------------------------------------------------
        // Date filter
        // --------------------------------------------------------

        if (date.HasValue)
        {
            var start =
                date.Value.ToDateTime(
                    TimeOnly.MinValue,
                    DateTimeKind.Utc);

            var end = start.AddDays(1);

            query = query.Where(
                x => x.AssignedAtUtc >= start &&
                     x.AssignedAtUtc < end);
        }


        var result = await query
            .OrderByDescending(x => x.AssignedAtUtc)
            .Select(x => new SalesLeadResponseDto
            {
                AssignmentId = x.Id,

                SalesLeadId = x.SalesLeadId,

                TenantId = x.TenantId ?? 0,

                BranchId = x.BranchId,

                BranchName =
                    x.Branch != null
                        ? x.Branch.BranchName
                        : string.Empty,

                PhoneNumber =
                    x.SalesLead.PhoneNumber,

                CustomerName =
                    x.SalesLead.CustomerName,

                AssignedToUserId =
                    x.AssignedToUserId,

                AssignedToUserName =
                    x.AssignedToUser.FullName,

                AssignedToEmployeeCode =
                    x.AssignedToUser.EmployeeCode,

                AssignedByUserId =
                    x.AssignedByUserId,

                AssignedByUserName =
                    x.AssignedByUser.FullName,

                AssignedAtUtc =
                    x.AssignedAtUtc,

                Outcome =
                    x.Outcome,

                Notes =
                    x.Notes,

                CompletedAtUtc =
                    x.CompletedAtUtc
            })
            .ToListAsync(cancellationToken);


        return Ok(result);
    }


    // ============================================================
    // GET MY LEADS
    // ============================================================


    [HttpGet("my")]
    public async Task<IActionResult> GetMyLeads(
    [FromQuery] SalesLeadOutcome? outcome,
    [FromQuery] DateOnly? date,
    CancellationToken cancellationToken = default)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId = _tenantService.UserId.Value;

        var query = _db.SalesLeadAssignments
            .AsNoTracking()
            .Where(x => x.AssignedToUserId == currentUserId);

        // ------------------------------------------------------------
        // Outcome filter
        // ------------------------------------------------------------
        if (outcome.HasValue)
        {
            query = query.Where(x => x.Outcome == outcome.Value);
        }

        // ------------------------------------------------------------
        // Date filter
        // ------------------------------------------------------------
        if (date.HasValue)
        {
            var start = date.Value.ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Utc);

            var end = start.AddDays(1);

            query = query.Where(x =>
                x.AssignedAtUtc >= start &&
                x.AssignedAtUtc < end);
        }

        // ------------------------------------------------------------
        // Project to response DTO
        // ------------------------------------------------------------
        var result = await query
            .OrderBy(x => x.Outcome)
            .ThenByDescending(x => x.AssignedAtUtc)
            .Select(x => new SalesLeadResponseDto
            {
                AssignmentId = x.Id,

                SalesLeadId = x.SalesLeadId,

                TenantId = x.TenantId ?? 0,

                BranchId = x.BranchId,

                BranchName = x.Branch.BranchName,

                PhoneNumber = x.SalesLead.PhoneNumber,

                CustomerName = x.SalesLead.CustomerName,

                AssignedToUserId = x.AssignedToUserId,

                AssignedToUserName = x.AssignedToUser.FullName,

                AssignedToEmployeeCode =
                    x.AssignedToUser.EmployeeCode,

                AssignedByUserId = x.AssignedByUserId,

                AssignedByUserName =
                    x.AssignedByUser.FullName,

                AssignedAtUtc = x.AssignedAtUtc,

                Outcome = x.Outcome,

                Notes = x.Notes,

                CompletedAtUtc = x.CompletedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(result);
    }


    // ============================================================
    // ASSIGN LEADS
    //
    // HR Ops
    // Branch Manager
    // Company Admin
    // Senior Sales
    // ============================================================

    [HttpPost("assign")]
    public async Task<IActionResult> AssignLeads(
        [FromBody] AssignSalesLeadsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var currentUser =
            await GetCurrentUserAsync(cancellationToken);

        if (currentUser == null)
            return Unauthorized();


        if (!CanAssignLeads(currentUser))
        {
            return Forbid();
        }


        if (request.BranchId <= 0)
        {
            return BadRequest(new
            {
                message = "A valid branch is required."
            });
        }


        if (request.AssignedToUserId <= 0)
        {
            return BadRequest(new
            {
                message =
                    "A valid sales employee is required."
            });
        }


        if (request.Leads == null ||
            request.Leads.Count == 0)
        {
            return BadRequest(new
            {
                message =
                    "At least one phone number is required."
            });
        }


        // --------------------------------------------------------
        // Validate branch access
        // --------------------------------------------------------

        if (!CanAccessBranch(
                currentUser,
                request.BranchId))
        {
            return Forbid();
        }


        // --------------------------------------------------------
        // Validate branch
        // --------------------------------------------------------

        var branch = await _db.TenantBranches
            .FirstOrDefaultAsync(
                x => x.Id == request.BranchId,
                cancellationToken);

        if (branch == null)
        {
            return BadRequest(new
            {
                message = "Branch not found."
            });
        }


        // --------------------------------------------------------
        // Validate assigned employee
        // --------------------------------------------------------

        var assignedUser =
            await _db.Users
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                        request.AssignedToUserId &&
                        x.IsActive,
                    cancellationToken);

        if (assignedUser == null)
        {
            return BadRequest(new
            {
                message =
                    "Assigned employee was not found or is inactive."
            });
        }


        // Must be Sales Staff.
        if (assignedUser.RoleNumber != 5)
        {
            return BadRequest(new
            {
                message =
                    "Leads can only be assigned to Sales Staff."
            });
        }


        // Employee must belong to requested branch.
        if (assignedUser.BranchId != request.BranchId)
        {
            return BadRequest(new
            {
                message =
                    "The selected sales employee does not belong to this branch."
            });
        }


        // --------------------------------------------------------
        // Senior sales can assign only to junior sales.
        // --------------------------------------------------------

        if (IsSeniorSales(currentUser))
        {
            var designation =
                assignedUser.Designation?.Trim();

            if (designation != null &&
                designation.Equals(
                    "senior",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message =
                        "Senior Sales can assign leads only to junior Sales Staff."
                });
            }
        }


        // --------------------------------------------------------
        // Clean and validate phone numbers
        // --------------------------------------------------------

        var cleanLeads =
            request.Leads
                .Where(x =>
                    !string.IsNullOrWhiteSpace(
                        x.PhoneNumber))
                .Select(x => new
                {
                    PhoneNumber =
                        x.PhoneNumber.Trim(),

                    CustomerName =
                        string.IsNullOrWhiteSpace(
                            x.CustomerName)
                            ? null
                            : x.CustomerName.Trim()
                })
                .GroupBy(x => x.PhoneNumber)
                .Select(x => x.First())
                .ToList();


        if (cleanLeads.Count == 0)
        {
            return BadRequest(new
            {
                message =
                    "No valid phone numbers were provided."
            });
        }


        // --------------------------------------------------------
        // Existing leads
        // --------------------------------------------------------

        var phoneNumbers =
            cleanLeads
                .Select(x => x.PhoneNumber)
                .ToList();


        var existingLeads =
            await _db.SalesLeads
                .Where(x =>
                    x.BranchId ==
                    request.BranchId &&
                    phoneNumbers.Contains(
                        x.PhoneNumber))
                .ToListAsync(cancellationToken);


        var existingByPhone =
            existingLeads.ToDictionary(
                x => x.PhoneNumber,
                StringComparer.OrdinalIgnoreCase);


        var newLeads =
            new List<SalesLead>();


        foreach (var item in cleanLeads)
        {
            if (existingByPhone.TryGetValue(
                    item.PhoneNumber,
                    out var existing))
            {
                // Update customer name only if a new one was supplied.
                if (!string.IsNullOrWhiteSpace(
                        item.CustomerName) &&
                    string.IsNullOrWhiteSpace(
                        existing.CustomerName))
                {
                    existing.CustomerName =
                        item.CustomerName;
                }

                existing.IsActive = true;
            }
            else
            {
                var lead = new SalesLead
                {
                    TenantId =
                        currentUser.TenantId,

                    BranchId =
                        request.BranchId,

                    PhoneNumber =
                        item.PhoneNumber,

                    CustomerName =
                        item.CustomerName,

                    IsActive = true
                };

                newLeads.Add(lead);
                existingByPhone[item.PhoneNumber] = lead;
            }
        }


        if (newLeads.Count > 0)
        {
            await _db.SalesLeads.AddRangeAsync(
                newLeads,
                cancellationToken);
        }


        // --------------------------------------------------------
        // Prevent duplicate pending assignment to same employee
        // --------------------------------------------------------

        var leadIds =
            existingByPhone.Values
                .Where(x => x.Id > 0)
                .Select(x => x.Id)
                .ToList();


        var existingAssignments =
            leadIds.Count == 0
                ? new List<SalesLeadAssignment>()
                : await _db.SalesLeadAssignments
                    .Where(x =>
                        leadIds.Contains(
                            x.SalesLeadId) &&
                        x.AssignedToUserId ==
                        request.AssignedToUserId &&
                        x.Outcome ==
                        SalesLeadOutcome.Pending)
                    .ToListAsync(cancellationToken);


        var existingPendingLeadIds =
            existingAssignments
                .Select(x => x.SalesLeadId)
                .ToHashSet();


        var now = DateTime.UtcNow;

        var assignments =
            new List<SalesLeadAssignment>();


        foreach (var lead in existingByPhone.Values)
        {
            // Newly-created lead doesn't have an ID until SaveChanges.
            // It can still be assigned after SaveChanges.
        }


        await _db.SaveChangesAsync(
            cancellationToken);


        foreach (var lead in existingByPhone.Values)
        {
            if (existingPendingLeadIds.Contains(
                    lead.Id))
            {
                continue;
            }

            assignments.Add(
                new SalesLeadAssignment
                {
                    TenantId =
                        currentUser.TenantId,

                    BranchId =
                        request.BranchId,

                    SalesLeadId =
                        lead.Id,

                    AssignedToUserId =
                        request.AssignedToUserId,

                    AssignedByUserId =
                        currentUser.Id,

                    AssignedAtUtc =
                        now,

                    Outcome =
                        SalesLeadOutcome.Pending
                });
        }


        if (assignments.Count == 0)
        {
            return Ok(new
            {
                message =
                    "All selected numbers are already assigned to this employee.",
                assignedCount = 0
            });
        }


        await _db.SalesLeadAssignments.AddRangeAsync(
            assignments,
            cancellationToken);


        await _db.SaveChangesAsync(
            cancellationToken);


        return Ok(new
        {
            message =
                "Sales leads assigned successfully.",

            assignedCount =
                assignments.Count
        });
    }


    // ============================================================
    // UPDATE OUTCOME
    //
    // Junior Sales updates their own assignment.
    // Management may also update if required.
    // ============================================================

    [HttpPut("{assignmentId:int}/outcome")]
    public async Task<IActionResult> UpdateOutcome(
        int assignmentId,
        [FromBody] UpdateSalesLeadOutcomeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var currentUser =
            await GetCurrentUserAsync(cancellationToken);

        if (currentUser == null)
            return Unauthorized();


        var assignment =
            await _db.SalesLeadAssignments
                .Include(x => x.SalesLead)
                .Include(x => x.Branch)
                .Include(x => x.AssignedToUser)
                .Include(x => x.AssignedByUser)
                .FirstOrDefaultAsync(
                    x => x.Id == assignmentId,
                    cancellationToken);


        if (assignment == null)
        {
            return NotFound(new
            {
                message =
                    "Sales lead assignment not found."
            });
        }


        var isOwner =
            assignment.AssignedToUserId ==
            currentUser.Id;


        var isManagement =
            IsManagementRole(currentUser);


        var isSeniorInSameBranch =
            IsSeniorSales(currentUser) &&
            currentUser.BranchId ==
            assignment.BranchId;


        if (!isOwner &&
            !isManagement &&
            !isSeniorInSameBranch)
        {
            return Forbid();
        }


        // --------------------------------------------------------
        // Valid outcome
        // --------------------------------------------------------

        //if (request.Outcome ==
        //    SalesLeadOutcome.Pending)
        //{
        //    return BadRequest(new
        //    {
        //        message =
        //            "Please select a completed call outcome."
        //    });
        //}


        assignment.Outcome =
            request.Outcome;


        assignment.Notes =
            string.IsNullOrWhiteSpace(request.Notes)
                ? null
                : request.Notes.Trim();


        assignment.CompletedAtUtc =
            DateTime.UtcNow;


        await _db.SaveChangesAsync(
            cancellationToken);


        return Ok(ToResponse(assignment));
    }


    // ============================================================
    // GET ASSIGNMENT DETAILS
    // ============================================================

    [HttpGet("{assignmentId:int}")]
    public async Task<IActionResult> GetAssignment(
        int assignmentId,
        CancellationToken cancellationToken = default)
    {
        var currentUser =
            await GetCurrentUserAsync(cancellationToken);

        if (currentUser == null)
            return Unauthorized();


        var assignment =
            await _db.SalesLeadAssignments
                .AsNoTracking()
                .Include(x => x.SalesLead)
                .Include(x => x.Branch)
                .Include(x => x.AssignedToUser)
                .Include(x => x.AssignedByUser)
                .FirstOrDefaultAsync(
                    x => x.Id == assignmentId,
                    cancellationToken);


        if (assignment == null)
            return NotFound();


        var allowed =
            assignment.AssignedToUserId ==
                currentUser.Id ||

            CanViewTeam(currentUser);


        if (!allowed)
            return Forbid();


        if (currentUser.RoleNumber == 4 ||
            IsSeniorSales(currentUser))
        {
            if (currentUser.BranchId !=
                assignment.BranchId)
            {
                return Forbid();
            }
        }


        return Ok(ToResponse(assignment));
    }


    // ============================================================
    // METRICS
    //
    // This is the important source of truth for Sales dashboard.
    // ============================================================

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics(
        [FromQuery] int? branchId,
        [FromQuery] int? userId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        var currentUser =
            await GetCurrentUserAsync(cancellationToken);

        if (currentUser == null)
            return Unauthorized();


        if (!CanViewTeam(currentUser) &&
            !IsSalesStaff(currentUser))
        {
            return Forbid();
        }


        var query =
            _db.SalesLeadAssignments
                .AsNoTracking()
                .AsQueryable();


        // --------------------------------------------------------
        // Employee scope
        // --------------------------------------------------------

        if (userId.HasValue)
        {
            if (!CanViewRequestedUser(
                    currentUser,
                    userId.Value))
            {
                return Forbid();
            }

            query = query.Where(
                x => x.AssignedToUserId ==
                     userId.Value);
        }
        else
        {
            // Normal sales staff only sees own metrics.
            if (IsSalesStaff(currentUser))
            {
                query = query.Where(
                    x => x.AssignedToUserId ==
                         currentUser.Id);
            }
        }


        // --------------------------------------------------------
        // Branch scope
        // --------------------------------------------------------

        if (branchId.HasValue)
        {
            if (!CanAccessBranch(
                    currentUser,
                    branchId.Value))
            {
                return Forbid();
            }

            query = query.Where(
                x => x.BranchId ==
                     branchId.Value);
        }
        else
        {
            if (currentUser.RoleNumber == 4 ||
                IsSeniorSales(currentUser))
            {
                if (!currentUser.BranchId.HasValue)
                    return BadRequest(new
                    {
                        message =
                            "Your account is not associated with a branch."
                    });

                query = query.Where(
                    x => x.BranchId ==
                         currentUser.BranchId.Value);
            }
        }


        // --------------------------------------------------------
        // Date
        // --------------------------------------------------------

        if (date.HasValue)
        {
            var start =
                date.Value.ToDateTime(
                    TimeOnly.MinValue,
                    DateTimeKind.Utc);

            var end = start.AddDays(1);

            query = query.Where(
                x => x.AssignedAtUtc >= start &&
                     x.AssignedAtUtc < end);
        }


        var outcomes =
            await query
                .GroupBy(x => x.Outcome)
                .Select(x => new
                {
                    Outcome = x.Key,
                    Count = x.Count()
                })
                .ToListAsync(cancellationToken);


        int Count(SalesLeadOutcome outcome)
        {
            return outcomes
                .FirstOrDefault(
                    x => x.Outcome == outcome)
                ?.Count ?? 0;
        }


        var pending =
            Count(SalesLeadOutcome.Pending);

        var connected =
            Count(SalesLeadOutcome.Connected);

        var respondedWell =
            Count(SalesLeadOutcome.RespondedWell);

        var followUp =
            Count(SalesLeadOutcome.FollowUp);

        var notConnected =
            Count(SalesLeadOutcome.NotConnected);


        // Connected is a parent category.
        var connectedTotal =
            connected +
            respondedWell +
            followUp;


        // Calls Made includes every assignment,
        // including pending.
        var callsMade =
            pending +
            connectedTotal +
            notConnected;


        var result = new SalesLeadMetricsDto
        {
            CallsMade = callsMade,

            Connected = connectedTotal,

            RespondedWell = respondedWell,

            FollowUp = followUp,

            NotConnected = notConnected,

            OtherConnected = connected,

            Pending = pending
        };


        return Ok(result);
    }


    // ============================================================
    // METRIC DETAILS
    //
    // Allows frontend cards to open actual phone numbers.
    //
    // Example:
    //
    // /api/SalesLeads/metrics/details
    //     ?metric=respondedWell
    // ============================================================

    [HttpGet("metrics/details")]
    public async Task<IActionResult> GetMetricDetails(
        [FromQuery] string metric,
        [FromQuery] int? branchId,
        [FromQuery] int? userId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        var currentUser =
            await GetCurrentUserAsync(cancellationToken);

        if (currentUser == null)
            return Unauthorized();


        if (!CanViewTeam(currentUser) &&
            !IsSalesStaff(currentUser))
        {
            return Forbid();
        }


        var query =
            _db.SalesLeadAssignments
                .AsNoTracking()
                .Include(x => x.SalesLead)
                .Include(x => x.Branch)
                .Include(x => x.AssignedToUser)
                .Include(x => x.AssignedByUser)
                .AsQueryable();


        // --------------------------------------------------------
        // User scope
        // --------------------------------------------------------

        if (userId.HasValue)
        {
            if (!CanViewRequestedUser(
                    currentUser,
                    userId.Value))
            {
                return Forbid();
            }

            query = query.Where(
                x => x.AssignedToUserId ==
                     userId.Value);
        }
        else if (IsSalesStaff(currentUser))
        {
            query = query.Where(
                x => x.AssignedToUserId ==
                     currentUser.Id);
        }


        // --------------------------------------------------------
        // Branch scope
        // --------------------------------------------------------

        if (branchId.HasValue)
        {
            if (!CanAccessBranch(
                    currentUser,
                    branchId.Value))
            {
                return Forbid();
            }

            query = query.Where(
                x => x.BranchId ==
                     branchId.Value);
        }
        else if (currentUser.RoleNumber == 4 ||
                 IsSeniorSales(currentUser))
        {
            if (!currentUser.BranchId.HasValue)
                return BadRequest(new
                {
                    message =
                        "Your account is not associated with a branch."
                });

            query = query.Where(
                x => x.BranchId ==
                     currentUser.BranchId.Value);
        }


        // --------------------------------------------------------
        // Date
        // --------------------------------------------------------

        if (date.HasValue)
        {
            var start =
                date.Value.ToDateTime(
                    TimeOnly.MinValue,
                    DateTimeKind.Utc);

            var end = start.AddDays(1);

            query = query.Where(
                x => x.AssignedAtUtc >= start &&
                     x.AssignedAtUtc < end);
        }


        // --------------------------------------------------------
        // Metric mapping
        // --------------------------------------------------------

        metric =
            metric.Trim()
                .ToLowerInvariant();


        switch (metric)
        {
            case "calls":
            case "callsmade":
                // No outcome filter.
                break;


            case "connected":
                query = query.Where(x =>
                    x.Outcome ==
                        SalesLeadOutcome.Connected ||
                    x.Outcome ==
                        SalesLeadOutcome.RespondedWell ||
                    x.Outcome ==
                        SalesLeadOutcome.FollowUp);
                break;


            case "respondedwell":
                query = query.Where(x =>
                    x.Outcome ==
                    SalesLeadOutcome.RespondedWell);
                break;


            case "followup":
                query = query.Where(x =>
                    x.Outcome ==
                    SalesLeadOutcome.FollowUp);
                break;


            case "notconnected":
                query = query.Where(x =>
                    x.Outcome ==
                    SalesLeadOutcome.NotConnected);
                break;


            case "otherconnected":
                query = query.Where(x =>
                    x.Outcome ==
                    SalesLeadOutcome.Connected);
                break;


            case "pending":
                query = query.Where(x =>
                    x.Outcome ==
                    SalesLeadOutcome.Pending);
                break;


            default:
                return BadRequest(new
                {
                    message =
                        "Invalid metric."
                });
        }


        var result =
            await query
                .OrderByDescending(
                    x => x.AssignedAtUtc)
                .Select(x => new SalesLeadResponseDto
                {
                    AssignmentId =
                        x.Id,

                    SalesLeadId =
                        x.SalesLeadId,

                    TenantId =
                        x.TenantId ?? 0,

                    BranchId =
                        x.BranchId,

                    BranchName =
                        x.Branch != null
                            ? x.Branch.BranchName
                            : string.Empty,

                    PhoneNumber =
                        x.SalesLead.PhoneNumber,

                    CustomerName =
                        x.SalesLead.CustomerName,

                    AssignedToUserId =
                        x.AssignedToUserId,

                    AssignedToUserName =
                        x.AssignedToUser.FullName,

                    AssignedToEmployeeCode =
                        x.AssignedToUser.EmployeeCode,

                    AssignedByUserId =
                        x.AssignedByUserId,

                    AssignedByUserName =
                        x.AssignedByUser.FullName,

                    AssignedAtUtc =
                        x.AssignedAtUtc,

                    Outcome =
                        x.Outcome,

                    Notes =
                        x.Notes,

                    CompletedAtUtc =
                        x.CompletedAtUtc
                })
                .ToListAsync(cancellationToken);


        return Ok(result);
    }


    // ============================================================
    // REQUESTED USER ACCESS
    // ============================================================

    private static bool CanViewRequestedUser(
        ApplicationUser currentUser,
        int requestedUserId)
    {
        // User can always see themselves.
        if (currentUser.Id ==
            requestedUserId)
        {
            return true;
        }

        // Company Admin / HR Ops can see tenant users.
        if (currentUser.RoleNumber == 2 ||
            currentUser.RoleNumber == 3)
        {
            return true;
        }

        // Branch Manager / Senior Sales can see
        // their branch team.
        if (currentUser.RoleNumber == 4 ||
            IsSeniorSales(currentUser))
        {
            return true;
        }

        return false;
    }


    // ============================================================
    // RESPONSE MAPPER
    // ============================================================

    private static SalesLeadResponseDto ToResponse(
        SalesLeadAssignment x)
    {
        return new SalesLeadResponseDto
        {
            AssignmentId =
                x.Id,

            SalesLeadId =
                x.SalesLeadId,

            TenantId =
                x.TenantId ?? 0,

            BranchId =
                x.BranchId,

            BranchName =
                x.Branch?.BranchName ??
                string.Empty,

            PhoneNumber =
                x.SalesLead.PhoneNumber,

            CustomerName =
                x.SalesLead.CustomerName,

            AssignedToUserId =
                x.AssignedToUserId,

            AssignedToUserName =
                x.AssignedToUser?.FullName ??
                string.Empty,

            AssignedToEmployeeCode =
                x.AssignedToUser?.EmployeeCode,

            AssignedByUserId =
                x.AssignedByUserId,

            AssignedByUserName =
                x.AssignedByUser?.FullName ??
                string.Empty,

            AssignedAtUtc =
                x.AssignedAtUtc,

            Outcome =
                x.Outcome,

            Notes =
                x.Notes,

            CompletedAtUtc =
                x.CompletedAtUtc
        };
    }
}