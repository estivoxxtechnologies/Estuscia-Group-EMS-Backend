using System.Security.Claims;
using System.Text.Json.Serialization;

using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Entities;
using Estuscia.Domain.Enums;
using Estuscia.Infrastructure.Persistence;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PayrollController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantService _tenantService;

    public PayrollController(
        AppDbContext db,
        ICurrentTenantService tenantService)
    {
        _db = db;
        _tenantService = tenantService;
    }

    // ============================================================
    // GET /api/Payroll/cycles
    // ============================================================

    [HttpGet("cycles")]
    public async Task<IActionResult> GetCycles(
        [FromQuery] int? year = null,
        [FromQuery] int? branchId = null)
    {
        if (!CanViewPayroll())
            return Forbid();

        var effectiveBranchId =
            await GetEffectiveBranchId(branchId);

        if (IsBranchManager() && !effectiveBranchId.HasValue)
        {
            return BadRequest(new
            {
                message = "Branch Manager has no assigned branch."
            });
        }

        var query = _db.PayrollCycles
            .AsNoTracking()
            .Include(x => x.PayrollRecords)
            .AsQueryable();

        if (year.HasValue)
        {
            query = query.Where(x =>
                x.Year == year.Value);
        }

        var cycles = await query
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .ToListAsync();

        var result = new List<PayrollCycleDto>();

        foreach (var cycle in cycles)
        {
            var visibleRecords =
                cycle.PayrollRecords.AsEnumerable();

            /*
             * Branch filtering cannot be done directly from
             * PayrollCycle because PayrollCycle does not contain
             * BranchId.
             */
            if (effectiveBranchId.HasValue)
            {
                var userIds = visibleRecords
                    .Select(x => x.UserId)
                    .Distinct()
                    .ToList();

                var allowedUserIds = await _db.Users
                    .AsNoTracking()
                    .Where(x =>
                        userIds.Contains(x.Id) &&
                        x.BranchId == effectiveBranchId.Value)
                    .Select(x => x.Id)
                    .ToListAsync();

                var allowedSet =
                    allowedUserIds.ToHashSet();

                visibleRecords = visibleRecords
                    .Where(x =>
                        allowedSet.Contains(x.UserId));
            }

            var visibleRecordList =
                visibleRecords.ToList();

            result.Add(new PayrollCycleDto
            {
                Id = cycle.Id,
                TenantId = cycle.TenantId,

                Year = cycle.Year,
                Month = cycle.Month,
                MonthYear = cycle.MonthYear,

                Status = cycle.Status.ToString(),

                TotalBasicSalary =
                    visibleRecordList.Sum(x => x.BasicSalary),

                TotalBonus =
                    visibleRecordList.Sum(x => x.TotalBonus),

                TotalDeduction =
                    visibleRecordList.Sum(x => x.TotalDeduction),

                TotalNetSalary =
                    visibleRecordList.Sum(x => x.NetSalary),

                SubmittedByUserId =
                    cycle.SubmittedByUserId,

                SubmittedAtUtc =
                    cycle.SubmittedAtUtc,

                ApprovedByUserId =
                    cycle.ApprovedByUserId,

                ApprovedAtUtc =
                    cycle.ApprovedAtUtc,

                RejectedByUserId =
                    cycle.RejectedByUserId,

                RejectedAtUtc =
                    cycle.RejectedAtUtc,

                RejectionReason =
                    cycle.RejectionReason,

                PaidByUserId =
                    cycle.PaidByUserId,

                PaidAtUtc =
                    cycle.PaidAtUtc,

                IsLocked =
                    cycle.IsLocked,

                EmployeeCount =
                    visibleRecordList.Count,

                PaidEmployeeCount =
                    visibleRecordList.Count(x =>
                        x.PaymentStatus ==
                        PayrollPaymentStatus.Paid),

                UnpaidEmployeeCount =
                    visibleRecordList.Count(x =>
                        x.PaymentStatus ==
                        PayrollPaymentStatus.Unpaid)
            });
        }

        return Ok(result);
    }

    // ============================================================
    // GET /api/Payroll/cycles/{id}
    // ============================================================

    [HttpGet("cycles/{id:int}")]
    public async Task<IActionResult> GetCycle(int id)
    {
        if (!CanViewPayroll())
            return Forbid();

        var cycle = await _db.PayrollCycles
            .AsNoTracking()
            .Include(x => x.PayrollRecords)
            .FirstOrDefaultAsync(x =>
                x.Id == id);

        if (cycle == null)
        {
            return NotFound(new
            {
                message = "Payroll cycle not found."
            });
        }

        if (!await CanViewCycleAsync(cycle))
            return Forbid();

        var effectiveBranchId =
            await GetEffectiveBranchId(null);

        var payrollRecords = cycle.PayrollRecords
            .OrderBy(x => x.UserId)
            .ToList();

        // --------------------------------------------------------
        // Branch filtering
        // --------------------------------------------------------

        if (effectiveBranchId.HasValue)
        {
            var recordUserIds = payrollRecords
                .Select(x => x.UserId)
                .Distinct()
                .ToList();

            var allowedUserIds = await _db.Users
                .AsNoTracking()
                .Where(x =>
                    recordUserIds.Contains(x.Id) &&
                    x.BranchId == effectiveBranchId.Value)
                .Select(x => x.Id)
                .ToListAsync();

            var allowedSet =
                allowedUserIds.ToHashSet();

            payrollRecords = payrollRecords
                .Where(x =>
                    allowedSet.Contains(x.UserId))
                .ToList();
        }

        // --------------------------------------------------------
        // Map records
        // --------------------------------------------------------

        var records = payrollRecords
            .Select(x => new PayrollRecordDto
            {
                Id = x.Id,
                PayrollCycleId = x.PayrollCycleId,
                UserId = x.UserId,

                BasicSalary = x.BasicSalary,
                TotalBonus = x.TotalBonus,
                TotalDeduction = x.TotalDeduction,
                NetSalary = x.NetSalary,

                Status = x.Status.ToString(),

                PaymentStatus =
                    x.PaymentStatus.ToString(),

                IsLocked = x.IsLocked,

                SubmittedByUserId =
                    x.SubmittedByUserId,

                SubmittedAtUtc =
                    x.SubmittedAtUtc,

                ApprovedByUserId =
                    x.ApprovedByUserId,

                ApprovedAtUtc =
                    x.ApprovedAtUtc,

                PaidByUserId =
                    x.PaidByUserId,

                PaidAtUtc =
                    x.PaidAtUtc
            })
            .ToList();

        // --------------------------------------------------------
        // Employee information
        // --------------------------------------------------------

        var userIds = records
            .Select(x => x.UserId)
            .Distinct()
            .ToList();

        var users = await _db.Users
            .AsNoTracking()
            .Where(x =>
                userIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.EmployeeCode,
                x.FullName,
                x.Designation,
                x.Department,
                x.BranchId
            })
            .ToListAsync();

        foreach (var record in records)
        {
            var user = users.FirstOrDefault(x =>
                x.Id == record.UserId);

            if (user == null)
                continue;

            record.EmployeeCode =
                user.EmployeeCode;

            record.EmployeeName =
                user.FullName?.Trim();

            record.Designation =
                user.Designation;

            record.Department =
                user.Department;

            record.BranchId =
                user.BranchId;
        }

        // --------------------------------------------------------
        // Visible totals
        // --------------------------------------------------------

        var visibleBasic =
            records.Sum(x => x.BasicSalary);

        var visibleBonus =
            records.Sum(x => x.TotalBonus);

        var visibleDeduction =
            records.Sum(x => x.TotalDeduction);

        var visibleNet =
            records.Sum(x => x.NetSalary);

        return Ok(new PayrollCycleDetailsDto
        {
            Id = cycle.Id,
            TenantId = cycle.TenantId,

            Year = cycle.Year,
            Month = cycle.Month,
            MonthYear = cycle.MonthYear,

            Status =
                cycle.Status.ToString(),

            TotalBasicSalary =
                visibleBasic,

            TotalBonus =
                visibleBonus,

            TotalDeduction =
                visibleDeduction,

            TotalNetSalary =
                visibleNet,

            SubmittedByUserId =
                cycle.SubmittedByUserId,

            SubmittedAtUtc =
                cycle.SubmittedAtUtc,

            ApprovedByUserId =
                cycle.ApprovedByUserId,

            ApprovedAtUtc =
                cycle.ApprovedAtUtc,

            RejectedByUserId =
                cycle.RejectedByUserId,

            RejectedAtUtc =
                cycle.RejectedAtUtc,

            RejectionReason =
                cycle.RejectionReason,

            PaidByUserId =
                cycle.PaidByUserId,

            PaidAtUtc =
                cycle.PaidAtUtc,

            IsLocked =
                cycle.IsLocked,

            EmployeeCount =
                records.Count,
            PaidEmployeeCount =
    records.Count(x =>
        x.PaymentStatus == PayrollPaymentStatus.Paid.ToString()),

            UnpaidEmployeeCount =
    records.Count(x =>
        x.PaymentStatus == PayrollPaymentStatus.Unpaid.ToString()),

            PayrollRecords =
                records
        });
    }

    // ============================================================
    // POST /api/Payroll/generate
    // ============================================================

    [HttpPost("generate")]
    public async Task<IActionResult> GeneratePayroll(
        [FromBody] GeneratePayrollRequest request)
    {
        if (!CanManagePayroll())
            return Forbid();

        if (request.Year < 2000 ||
            request.Year > 2100)
        {
            return BadRequest(new
            {
                message = "Invalid payroll year."
            });
        }

        if (request.Month < 1 ||
            request.Month > 12)
        {
            return BadRequest(new
            {
                message = "Invalid payroll month."
            });
        }

        var tenantId =
            _tenantService.TenantId;

        if (!tenantId.HasValue)
        {
            return BadRequest(new
            {
                message =
                    "A tenant must be selected before generating payroll."
            });
        }

        var effectiveBranchId =
            await GetEffectiveBranchId(
                request.BranchId);

        if (IsBranchManager())
            return Forbid();

        // --------------------------------------------------------
        // Prevent duplicate month payroll
        // --------------------------------------------------------

        var exists = await _db.PayrollCycles
            .AnyAsync(x =>
                x.TenantId == tenantId &&
                x.Year == request.Year &&
                x.Month == request.Month);

        if (exists)
        {
            return Conflict(new
            {
                message =
                    "Payroll already exists for this month."
            });
        }

        var monthYear =
            new DateTime(
                request.Year,
                request.Month,
                1)
            .ToString("MMMM yyyy");

        // --------------------------------------------------------
        // Employees
        // --------------------------------------------------------

        var usersQuery = _db.Users
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.IsActive);

        if (effectiveBranchId.HasValue)
        {
            usersQuery = usersQuery
                .Where(x =>
                    x.BranchId ==
                    effectiveBranchId.Value);
        }

        var users = await usersQuery
            .Select(x => new
            {
                x.Id,
                x.SalaryBase,
                x.BranchId
            })
            .ToListAsync();

        if (users.Count == 0)
        {
            return BadRequest(new
            {
                message =
                    effectiveBranchId.HasValue
                        ? "No active employees were found in the selected branch."
                        : "No active employees were found for payroll."
            });
        }

        // --------------------------------------------------------
        // Create cycle
        // --------------------------------------------------------

        var cycle = new PayrollCycle
        {
            TenantId =
                tenantId.Value,

            Year =
                request.Year,

            Month =
                request.Month,

            MonthYear =
                monthYear,

            Status =
                PayrollCycleStatus.Draft,

            TotalBasicSalary = 0,
            TotalBonus = 0,
            TotalDeduction = 0,
            TotalNetSalary = 0,

            IsLocked = false
        };

        foreach (var user in users)
        {
            var record = new Estuscia.Domain.Entities.PayrollRecord
            {
                TenantId =
                    tenantId.Value,

                PayrollCycle =
                    cycle,

                UserId =
                    user.Id,

                BasicSalary =
                    user.SalaryBase,

                TotalBonus = 0,
                TotalDeduction = 0,

                NetSalary =
                    user.SalaryBase,

                Status =
                    PayrollCycleStatus.Draft,

                PaymentStatus =
                    PayrollPaymentStatus.Unpaid,

                IsLocked = false
            };

            cycle.PayrollRecords.Add(record);
        }

        RecalculateCycle(cycle);

        _db.PayrollCycles.Add(cycle);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Payroll generated successfully.",

            cycleId =
                cycle.Id,

            branchId =
                effectiveBranchId
        });
    }

    // ============================================================
    // POST /api/Payroll/{id}/submit
    // ============================================================

    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> SubmitPayroll(int id)
    {
        if (!CanSubmitPayroll())
            return Forbid();

        var cycle = await _db.PayrollCycles
            .Include(x => x.PayrollRecords)
            .FirstOrDefaultAsync(x =>
                x.Id == id);

        if (cycle == null)
        {
            return NotFound(new
            {
                message =
                    "Payroll cycle not found."
            });
        }

        if (!await CanViewCycleAsync(cycle))
            return Forbid();

        if (cycle.IsLocked)
        {
            return BadRequest(new
            {
                message =
                    "Locked payroll cannot be submitted."
            });
        }

        if (cycle.Status !=
                PayrollCycleStatus.Draft &&
            cycle.Status !=
                PayrollCycleStatus.Rejected)
        {
            return BadRequest(new
            {
                message =
                    $"Payroll cannot be submitted from {cycle.Status} status."
            });
        }

        if (cycle.PayrollRecords.Count == 0)
        {
            return BadRequest(new
            {
                message =
                    "Payroll contains no employee records."
            });
        }

        var pendingAdjustments =
            await _db.PayrollAdjustments
                .AnyAsync(x =>
                    x.PayrollCycleId == cycle.Id &&
                    x.Status ==
                    PayrollAdjustmentStatus.PendingApproval);

        if (pendingAdjustments)
        {
            return BadRequest(new
            {
                message =
                    "All payroll adjustments must be approved or rejected before submitting payroll."
            });
        }

        var currentUserId =
            GetCurrentUserId();

        var now =
            DateTime.UtcNow;

        cycle.Status =
            PayrollCycleStatus.SubmittedByHR;

        cycle.SubmittedByUserId =
            currentUserId;

        cycle.SubmittedAtUtc =
            now;

        cycle.RejectedByUserId = null;
        cycle.RejectedAtUtc = null;
        cycle.RejectionReason = null;

        foreach (var record in cycle.PayrollRecords)
        {
            record.Status =
                PayrollCycleStatus.SubmittedByHR;

            record.SubmittedByUserId =
                currentUserId;

            record.SubmittedAtUtc =
                now;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Payroll submitted for Company Admin approval."
        });
    }

    // ============================================================
    // POST /api/Payroll/{id}/approve
    // ============================================================

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> ApprovePayroll(int id)
    {
        if (!IsCompanyAdmin() &&
            !IsSuperAdmin())
        {
            return Forbid();
        }

        var cycle = await _db.PayrollCycles
            .Include(x => x.PayrollRecords)
            .FirstOrDefaultAsync(x =>
                x.Id == id);

        if (cycle == null)
        {
            return NotFound(new
            {
                message =
                    "Payroll cycle not found."
            });
        }

        if (!await CanViewCycleAsync(cycle))
            return Forbid();

        if (cycle.IsLocked)
        {
            return BadRequest(new
            {
                message =
                    "Payroll is already locked."
            });
        }

        if (cycle.Status !=
            PayrollCycleStatus.SubmittedByHR)
        {
            return BadRequest(new
            {
                message =
                    $"Only submitted payroll can be approved. Current status: {cycle.Status}."
            });
        }

        var pendingAdjustments =
            await _db.PayrollAdjustments
                .AnyAsync(x =>
                    x.PayrollCycleId == cycle.Id &&
                    x.Status ==
                    PayrollAdjustmentStatus.PendingApproval);

        if (pendingAdjustments)
        {
            return BadRequest(new
            {
                message =
                    "There are pending payroll adjustments. Approve or reject them first."
            });
        }

        var rejectedAdjustments =
            await _db.PayrollAdjustments
                .AnyAsync(x =>
                    x.PayrollCycleId == cycle.Id &&
                    x.Status ==
                    PayrollAdjustmentStatus.Rejected);

        if (rejectedAdjustments)
        {
            return BadRequest(new
            {
                message =
                    "Rejected adjustments must be removed or corrected before payroll approval."
            });
        }

        var currentUserId =
            GetCurrentUserId();

        var now =
            DateTime.UtcNow;

        cycle.Status =
            PayrollCycleStatus.ApprovedByCompanyAdmin;

        cycle.ApprovedByUserId =
            currentUserId;

        cycle.ApprovedAtUtc =
            now;

        foreach (var record in cycle.PayrollRecords)
        {
            record.Status =
                PayrollCycleStatus.ApprovedByCompanyAdmin;

            record.ApprovedByUserId =
                currentUserId;

            record.ApprovedAtUtc =
                now;

            /*
             * PaymentStatus must remain Unpaid here.
             */
            record.PaymentStatus =
                PayrollPaymentStatus.Unpaid;

            record.IsLocked = false;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Payroll approved successfully."
        });
    }

    // ============================================================
    // POST /api/Payroll/{id}/reject
    // ============================================================

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> RejectPayroll(
        int id,
        [FromBody] RejectPayrollRequest request)
    {
        if (!IsCompanyAdmin() &&
            !IsSuperAdmin())
        {
            return Forbid();
        }

        var cycle = await _db.PayrollCycles
            .Include(x => x.PayrollRecords)
            .FirstOrDefaultAsync(x =>
                x.Id == id);

        if (cycle == null)
        {
            return NotFound(new
            {
                message =
                    "Payroll cycle not found."
            });
        }

        if (!await CanViewCycleAsync(cycle))
            return Forbid();

        if (cycle.IsLocked)
        {
            return BadRequest(new
            {
                message =
                    "Locked payroll cannot be rejected."
            });
        }

        if (cycle.Status !=
            PayrollCycleStatus.SubmittedByHR)
        {
            return BadRequest(new
            {
                message =
                    "Only submitted payroll can be rejected."
            });
        }

        var now =
            DateTime.UtcNow;

        cycle.Status =
            PayrollCycleStatus.Rejected;

        cycle.RejectedByUserId =
            GetCurrentUserId();

        cycle.RejectedAtUtc =
            now;

        cycle.RejectionReason =
            request.Reason?.Trim();

        foreach (var record in cycle.PayrollRecords)
        {
            record.Status =
                PayrollCycleStatus.Rejected;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Payroll rejected.",

            reason =
                request.Reason
        });
    }

    // ============================================================
    // POST /api/Payroll/{id}/pay
    //
    // Selected employee payment.
    //
    // Example:
    //
    // {
    //   "payrollRecordIds": [1, 2, 5]
    // }
    //
    // Only selected unpaid employees are paid.
    //
    // If employees remain unpaid:
    //
    // Cycle = ApprovedByCompanyAdmin
    // IsLocked = false
    //
    // When everybody is paid:
    //
    // Cycle = Paid
    // IsLocked = true
    // ============================================================

    [HttpPost("{id:int}/pay")]
    public async Task<IActionResult> PayPayroll(
        int id,
        [FromBody] PayPayrollRequest request)
    {
        if (!CanProcessPayment())
            return Forbid();

        if (request == null ||
            request.PayrollRecordIds == null ||
            request.PayrollRecordIds.Count == 0)
        {
            return BadRequest(new
            {
                message =
                    "Select at least one employee for payment."
            });
        }

        var requestedIds =
            request.PayrollRecordIds
                .Distinct()
                .ToHashSet();

        if (requestedIds.Count == 0)
        {
            return BadRequest(new
            {
                message =
                    "Select at least one employee for payment."
            });
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        try
        {
            var cycle =
                await _db.PayrollCycles
                    .Include(x => x.PayrollRecords)
                    .FirstOrDefaultAsync(x =>
                        x.Id == id);

            if (cycle == null)
            {
                await transaction.RollbackAsync();

                return NotFound(new
                {
                    message =
                        "Payroll cycle not found."
                });
            }

            if (!await CanViewCycleAsync(cycle))
            {
                await transaction.RollbackAsync();
                return Forbid();
            }

            if (cycle.IsLocked)
            {
                await transaction.RollbackAsync();

                return BadRequest(new
                {
                    message =
                        "Payroll is already fully paid and locked."
                });
            }

            if (cycle.Status !=
                PayrollCycleStatus.ApprovedByCompanyAdmin)
            {
                await transaction.RollbackAsync();

                return BadRequest(new
                {
                    message =
                        "Payroll must be approved by Company Admin before payment."
                });
            }

            // ----------------------------------------------------
            // Find selected records
            // ----------------------------------------------------

            var selectedRecords =
                cycle.PayrollRecords
                    .Where(x =>
                        requestedIds.Contains(x.Id))
                    .ToList();

            if (selectedRecords.Count == 0)
            {
                await transaction.RollbackAsync();

                return BadRequest(new
                {
                    message =
                        "None of the selected employees belong to this payroll cycle."
                });
            }

            // ----------------------------------------------------
            // Branch security
            // ----------------------------------------------------

            var effectiveBranchId =
                await GetEffectiveBranchId(null);

            if (effectiveBranchId.HasValue)
            {
                var selectedUserIds =
                    selectedRecords
                        .Select(x => x.UserId)
                        .Distinct()
                        .ToList();

                var allowedUserIds =
                    await _db.Users
                        .AsNoTracking()
                        .Where(x =>
                            selectedUserIds.Contains(x.Id) &&
                            x.BranchId ==
                                effectiveBranchId.Value)
                        .Select(x => x.Id)
                        .ToListAsync();

                var allowedSet =
                    allowedUserIds.ToHashSet();

                var unauthorizedRecord =
                    selectedRecords.FirstOrDefault(x =>
                        !allowedSet.Contains(x.UserId));

                if (unauthorizedRecord != null)
                {
                    await transaction.RollbackAsync();
                    return Forbid();
                }
            }

            // ----------------------------------------------------
            // Only approved + unpaid + unlocked records
            // ----------------------------------------------------

            var payableRecords =
                selectedRecords
                    .Where(x =>
                        x.Status ==
                            PayrollCycleStatus.ApprovedByCompanyAdmin &&
                        x.PaymentStatus ==
                            PayrollPaymentStatus.Unpaid &&
                        !x.IsLocked)
                    .ToList();

            if (payableRecords.Count == 0)
            {
                await transaction.RollbackAsync();

                return BadRequest(new
                {
                    message =
                        "All selected employees have already been paid or are not eligible for payment."
                });
            }

            var currentUserId =
                GetCurrentUserId();

            if (!currentUserId.HasValue)
            {
                await transaction.RollbackAsync();
                return Unauthorized();
            }

            var paidAt =
                DateTime.UtcNow;

            // ----------------------------------------------------
            // Mark selected employees as Paid
            // ----------------------------------------------------

            foreach (var record in payableRecords)
            {
                record.PaymentStatus =
                    PayrollPaymentStatus.Paid;

                record.PaidByUserId =
                    currentUserId.Value;

                record.PaidAtUtc =
                    paidAt;

                /*
                 * IMPORTANT:
                 *
                 * Do NOT lock individual records yet.
                 *
                 * The entire payroll is locked only when every
                 * employee has been paid.
                 */
                record.IsLocked = false;
            }

            // ----------------------------------------------------
            // Check whether ALL employees are paid
            // ----------------------------------------------------

            var allRecordsPaid =
                cycle.PayrollRecords.Count > 0 &&
                cycle.PayrollRecords.All(x =>
                    x.PaymentStatus ==
                    PayrollPaymentStatus.Paid);

            if (allRecordsPaid)
            {
                // =================================================
                // FINAL PAYMENT
                // =================================================

                cycle.Status =
                    PayrollCycleStatus.Paid;

                cycle.PaidByUserId =
                    currentUserId.Value;

                cycle.PaidAtUtc =
                    paidAt;

                cycle.IsLocked = true;

                foreach (var record in cycle.PayrollRecords)
                {
                    record.Status =
                        PayrollCycleStatus.Paid;

                    record.IsLocked = true;
                }
            }
            else
            {
                // =================================================
                // PARTIAL PAYMENT
                // =================================================

                cycle.Status =
                    PayrollCycleStatus.ApprovedByCompanyAdmin;

                cycle.IsLocked = false;

                /*
                 * Do not set PaidByUserId/PaidAtUtc on the cycle
                 * during partial payment.
                 *
                 * Those fields represent final cycle completion.
                 */
            }

            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            var paidCount =
                cycle.PayrollRecords.Count(x =>
                    x.PaymentStatus ==
                    PayrollPaymentStatus.Paid);

            var unpaidCount =
                cycle.PayrollRecords.Count(x =>
                    x.PaymentStatus ==
                    PayrollPaymentStatus.Unpaid);

            return Ok(new
            {
                message =
                    allRecordsPaid
                        ? "All payroll employees have been paid. Payroll is now permanently locked."
                        : "Selected employees have been marked as paid.",

                paidRecordIds =
                    payableRecords
                        .Select(x => x.Id)
                        .ToList(),

                paidCount,

                unpaidCount,

                totalEmployees =
                    cycle.PayrollRecords.Count,

                payrollCompleted =
                    allRecordsPaid,

                isLocked =
                    cycle.IsLocked,

                cycleStatus =
                    cycle.Status.ToString()
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // GET /api/Payroll/my-payslips
    // ============================================================

    [HttpGet("my-payslips")]
    public async Task<IActionResult> GetMyPayslips()
    {
        var userId =
            GetCurrentUserId();

        if (!userId.HasValue)
            return Unauthorized();

        var records =
            await _db.PayrollRecords
                .AsNoTracking()
                .Include(x => x.PayrollCycle)
                .Where(x =>
                    x.UserId == userId.Value &&
                    x.IsLocked)
                .OrderByDescending(x =>
                    x.PayrollCycle!.Year)
                .ThenByDescending(x =>
                    x.PayrollCycle!.Month)
                .ToListAsync();

        var result =
            await BuildPayslipDtos(records);

        return Ok(result);
    }

    // ============================================================
    // GET /api/Payroll/payslips
    // ============================================================

    [HttpGet("payslips")]
    public async Task<IActionResult> GetPayslips(
        [FromQuery] int? cycleId = null,
        [FromQuery] int? userId = null,
        [FromQuery] int? branchId = null)
    {
        if (!CanViewAllPayslips())
            return Forbid();

        var effectiveBranchId =
            await GetEffectiveBranchId(branchId);

        if (IsBranchManager() &&
            !effectiveBranchId.HasValue)
        {
            return BadRequest(new
            {
                message =
                    "Branch Manager has no assigned branch."
            });
        }

        var query =
            _db.PayrollRecords
                .AsNoTracking()
                .Include(x => x.PayrollCycle)
                .Where(x =>
                    x.IsLocked)
                .AsQueryable();

        if (cycleId.HasValue)
        {
            query = query.Where(x =>
                x.PayrollCycleId ==
                cycleId.Value);
        }

        if (userId.HasValue)
        {
            query = query.Where(x =>
                x.UserId ==
                userId.Value);
        }

        var records =
            await query
                .OrderByDescending(x =>
                    x.PayrollCycle!.Year)
                .ThenByDescending(x =>
                    x.PayrollCycle!.Month)
                .ToListAsync();

        var userIds =
            records
                .Select(x => x.UserId)
                .Distinct()
                .ToList();

        var usersQuery =
            _db.Users
                .AsNoTracking()
                .Where(x =>
                    userIds.Contains(x.Id));

        if (effectiveBranchId.HasValue)
        {
            usersQuery =
                usersQuery.Where(x =>
                    x.BranchId ==
                    effectiveBranchId.Value);
        }

        var users =
            await usersQuery
                .Select(x => new
                {
                    x.Id,
                    x.EmployeeCode,
                    x.FullName,
                    x.Designation,
                    x.Department,
                    x.BranchId
                })
                .ToListAsync();

        var allowedUserIds =
            users
                .Select(x => x.Id)
                .ToHashSet();

        records =
            records
                .Where(x =>
                    allowedUserIds.Contains(
                        x.UserId))
                .ToList();

        var result =
            await BuildPayslipDtos(
                records,
                users);

        return Ok(result);
    }

    // ============================================================
    // GET /api/Payroll/payslips/{recordId}
    // ============================================================

    [HttpGet("payslips/{recordId:int}")]
    public async Task<IActionResult> GetPayslip(
        int recordId)
    {
        var record =
            await _db.PayrollRecords
                .AsNoTracking()
                .Include(x => x.PayrollCycle)
                .FirstOrDefaultAsync(x =>
                    x.Id == recordId);

        if (record == null)
        {
            return NotFound(new
            {
                message =
                    "Payslip not found."
            });
        }

        if (!record.IsLocked)
        {
            return BadRequest(new
            {
                message =
                    "Payslip is available only after payroll has been paid and locked."
            });
        }

        if (!await CanViewPayslipAsync(record))
            return Forbid();

        var user =
            await _db.Users
                .AsNoTracking()
                .Where(x =>
                    x.Id == record.UserId)
                .Select(x => new
                {
                    x.Id,
                    x.EmployeeCode,
                    x.FullName,
                    x.Designation,
                    x.Department,
                    x.BranchId
                })
                .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound(new
            {
                message =
                    "Employee not found."
            });
        }

        var adjustments =
            await _db.PayrollAdjustments
                .AsNoTracking()
                .Where(x =>
                    x.PayrollCycleId ==
                        record.PayrollCycleId &&
                    x.UserId ==
                        record.UserId &&
                    x.Status ==
                        PayrollAdjustmentStatus.ApprovedByCompanyAdmin)
                .Select(x => new PayslipAdjustmentDto
                {
                    Id = x.Id,
                    Type = x.Type.ToString(),
                    Amount = x.Amount,
                    Reason = x.Reason
                })
                .ToListAsync();

        return Ok(new PayslipDto
        {
            Id =
                record.Id,

            PayrollCycleId =
                record.PayrollCycleId,

            UserId =
                record.UserId,

            EmployeeCode =
                user.EmployeeCode,

            EmployeeName =
                user.FullName?.Trim(),

            Designation =
                user.Designation,

            Department =
                user.Department,

            BranchId =
                user.BranchId,

            MonthYear =
                record.PayrollCycle?.MonthYear ??
                "",

            BasicSalary =
                record.BasicSalary,

            TotalBonus =
                record.TotalBonus,

            TotalDeduction =
                record.TotalDeduction,

            NetSalary =
                record.NetSalary,

            Status =
                record.Status.ToString(),

            IsLocked =
                record.IsLocked,

            PaidAtUtc =
                record.PaidAtUtc,

            Adjustments =
                adjustments
        });
    }

    // ============================================================
    // POST /api/Payroll/{cycleId}/adjustments
    // ============================================================

    [HttpPost("{cycleId:int}/adjustments")]
    public async Task<IActionResult> AddAdjustment(
        int cycleId,
        [FromBody] CreatePayrollAdjustmentRequest request)
    {
        if (!CanManagePayroll())
            return Forbid();

        if (request.Amount <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Adjustment amount must be greater than zero."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new
            {
                message =
                    "Adjustment reason is required."
            });
        }

        var cycle =
            await _db.PayrollCycles
                .Include(x => x.PayrollRecords)
                .FirstOrDefaultAsync(x =>
                    x.Id == cycleId);

        if (cycle == null)
        {
            return NotFound(new
            {
                message =
                    "Payroll cycle not found."
            });
        }

        if (!await CanViewCycleAsync(cycle))
            return Forbid();

        if (cycle.IsLocked)
        {
            return BadRequest(new
            {
                message =
                    "Locked payroll cannot be modified."
            });
        }

        if (cycle.Status !=
                PayrollCycleStatus.Draft &&
            cycle.Status !=
                PayrollCycleStatus.Rejected)
        {
            return BadRequest(new
            {
                message =
                    "Adjustments can only be added while payroll is Draft or Rejected."
            });
        }

        var employee =
            await _db.Users
                .AsNoTracking()
                .Where(x =>
                    x.Id == request.UserId &&
                    x.IsActive)
                .Select(x => new
                {
                    x.Id,
                    x.BranchId
                })
                .FirstOrDefaultAsync();

        if (employee == null)
        {
            return BadRequest(new
            {
                message =
                    "Employee not found or inactive."
            });
        }

        var effectiveBranchId =
            await GetEffectiveBranchId(null);

        if (effectiveBranchId.HasValue &&
            employee.BranchId !=
            effectiveBranchId.Value)
        {
            return Forbid();
        }

        var record =
            cycle.PayrollRecords
                .FirstOrDefault(x =>
                    x.UserId ==
                    request.UserId);

        if (record == null)
        {
            return BadRequest(new
            {
                message =
                    "Employee does not belong to this payroll cycle."
            });
        }

        var adjustment =
            new PayrollAdjustment
            {
                TenantId =
                    cycle.TenantId,

                PayrollCycleId =
                    cycle.Id,

                UserId =
                    request.UserId,

                Type =
                    request.Type,

                Amount =
                    request.Amount,

                Reason =
                    request.Reason.Trim(),

                Status =
                    PayrollAdjustmentStatus.PendingApproval
            };

        _db.PayrollAdjustments.Add(
            adjustment);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Payroll adjustment created and sent for approval.",

            adjustmentId =
                adjustment.Id
        });
    }

    // ============================================================
    // GET /api/Payroll/{cycleId}/adjustments
    // ============================================================

    [HttpGet("{cycleId:int}/adjustments")]
    public async Task<IActionResult> GetAdjustments(
        int cycleId)
    {
        if (!CanViewPayroll())
            return Forbid();

        var cycle =
            await _db.PayrollCycles
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == cycleId);

        if (cycle == null)
        {
            return NotFound(new
            {
                message =
                    "Payroll cycle not found."
            });
        }

        if (!await CanViewCycleAsync(cycle))
            return Forbid();

        var adjustments =
            await _db.PayrollAdjustments
                .AsNoTracking()
                .Where(x =>
                    x.PayrollCycleId ==
                    cycleId)
                .OrderByDescending(x =>
                    x.CreatedAtUtc)
                .Select(x =>
                    new PayrollAdjustmentDto
                    {
                        Id = x.Id,

                        PayrollCycleId =
                            x.PayrollCycleId,

                        UserId =
                            x.UserId,

                        Type =
                            x.Type.ToString(),

                        Amount =
                            x.Amount,

                        Reason =
                            x.Reason,

                        Status =
                            x.Status.ToString(),

                        ApprovedByUserId =
                            x.ApprovedByUserId,

                        ApprovedAtUtc =
                            x.ApprovedAtUtc,

                        RejectedByUserId =
                            x.RejectedByUserId,

                        RejectedAtUtc =
                            x.RejectedAtUtc,

                        RejectionReason =
                            x.RejectionReason
                    })
                .ToListAsync();

        var userIds =
            adjustments
                .Select(x => x.UserId)
                .Distinct()
                .ToList();

        var usersQuery =
            _db.Users
                .AsNoTracking()
                .Where(x =>
                    userIds.Contains(x.Id));

        var effectiveBranchId =
            await GetEffectiveBranchId(null);

        if (effectiveBranchId.HasValue)
        {
            usersQuery =
                usersQuery.Where(x =>
                    x.BranchId ==
                    effectiveBranchId.Value);
        }

        var users =
            await usersQuery
                .Select(x => new
                {
                    x.Id,
                    x.EmployeeCode,
                    x.FullName
                })
                .ToListAsync();

        var allowedUserIds =
            users
                .Select(x => x.Id)
                .ToHashSet();

        adjustments =
            adjustments
                .Where(x =>
                    allowedUserIds.Contains(
                        x.UserId))
                .ToList();

        foreach (var adjustment in adjustments)
        {
            var user =
                users.FirstOrDefault(x =>
                    x.Id ==
                    adjustment.UserId);

            if (user == null)
                continue;

            adjustment.EmployeeCode =
                user.EmployeeCode;

            adjustment.EmployeeName =
                user.FullName?.Trim();
        }

        return Ok(adjustments);
    }

    // ============================================================
    // POST /api/Payroll/adjustments/{id}/approve
    // ============================================================

    [HttpPost("adjustments/{id:int}/approve")]
    public async Task<IActionResult> ApproveAdjustment(
        int id)
    {
        if (!IsCompanyAdmin() &&
            !IsSuperAdmin())
        {
            return Forbid();
        }

        var adjustment =
            await _db.PayrollAdjustments
                .FirstOrDefaultAsync(x =>
                    x.Id == id);

        if (adjustment == null)
        {
            return NotFound(new
            {
                message =
                    "Payroll adjustment not found."
            });
        }

        var cycle =
            await _db.PayrollCycles
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                    adjustment.PayrollCycleId);

        if (cycle == null)
        {
            return NotFound(new
            {
                message =
                    "Payroll cycle not found."
            });
        }

        if (!await CanViewCycleAsync(cycle))
            return Forbid();

        if (cycle.IsLocked)
        {
            return BadRequest(new
            {
                message =
                    "Locked payroll cannot be modified."
            });
        }

        if (adjustment.Status !=
            PayrollAdjustmentStatus.PendingApproval)
        {
            return BadRequest(new
            {
                message =
                    "Only pending adjustments can be approved."
            });
        }

        adjustment.Status =
            PayrollAdjustmentStatus.ApprovedByCompanyAdmin;

        adjustment.ApprovedByUserId =
            GetCurrentUserId();

        adjustment.ApprovedAtUtc =
            DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await RecalculatePayrollCycle(
            cycle.Id);

        return Ok(new
        {
            message =
                "Payroll adjustment approved."
        });
    }

    // ============================================================
    // POST /api/Payroll/adjustments/{id}/reject
    // ============================================================

    [HttpPost("adjustments/{id:int}/reject")]
    public async Task<IActionResult> RejectAdjustment(
        int id,
        [FromBody] RejectPayrollRequest request)
    {
        if (!IsCompanyAdmin() &&
            !IsSuperAdmin())
        {
            return Forbid();
        }

        var adjustment =
            await _db.PayrollAdjustments
                .FirstOrDefaultAsync(x =>
                    x.Id == id);

        if (adjustment == null)
        {
            return NotFound(new
            {
                message =
                    "Payroll adjustment not found."
            });
        }

        var cycle =
            await _db.PayrollCycles
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                    adjustment.PayrollCycleId);

        if (cycle == null)
        {
            return NotFound(new
            {
                message =
                    "Payroll cycle not found."
            });
        }

        if (!await CanViewCycleAsync(cycle))
            return Forbid();

        if (adjustment.Status !=
            PayrollAdjustmentStatus.PendingApproval)
        {
            return BadRequest(new
            {
                message =
                    "Only pending adjustments can be rejected."
            });
        }

        adjustment.Status =
            PayrollAdjustmentStatus.Rejected;

        adjustment.RejectedByUserId =
            GetCurrentUserId();

        adjustment.RejectedAtUtc =
            DateTime.UtcNow;

        adjustment.RejectionReason =
            request.Reason?.Trim();

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Payroll adjustment rejected."
        });
    }

    // ============================================================
    // DELETE /api/Payroll/adjustments/{id}
    // ============================================================

    [HttpDelete("adjustments/{id:int}")]
    public async Task<IActionResult> DeleteAdjustment(
        int id)
    {
        if (!CanManagePayroll())
            return Forbid();

        var adjustment =
            await _db.PayrollAdjustments
                .FirstOrDefaultAsync(x =>
                    x.Id == id);

        if (adjustment == null)
        {
            return NotFound(new
            {
                message =
                    "Payroll adjustment not found."
            });
        }

        if (adjustment.Status !=
            PayrollAdjustmentStatus.PendingApproval)
        {
            return BadRequest(new
            {
                message =
                    "Only pending adjustments can be deleted."
            });
        }

        var cycle =
            await _db.PayrollCycles
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                    adjustment.PayrollCycleId);

        if (cycle == null)
        {
            return NotFound(new
            {
                message =
                    "Payroll cycle not found."
            });
        }

        if (!await CanViewCycleAsync(cycle))
            return Forbid();

        if (cycle.IsLocked)
        {
            return BadRequest(new
            {
                message =
                    "Locked payroll cannot be modified."
            });
        }

        var cycleId =
            adjustment.PayrollCycleId;

        _db.PayrollAdjustments.Remove(
            adjustment);

        await _db.SaveChangesAsync();

        await RecalculatePayrollCycle(
            cycleId);

        return Ok(new
        {
            message =
                "Payroll adjustment deleted."
        });
    }

    // ============================================================
    // DELETE /api/Payroll/{id}
    // ============================================================

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeletePayroll(
        int id)
    {
        if (!CanManagePayroll())
            return Forbid();

        var cycle =
            await _db.PayrollCycles
                .Include(x => x.PayrollRecords)
                .FirstOrDefaultAsync(x =>
                    x.Id == id);

        if (cycle == null)
        {
            return NotFound(new
            {
                message =
                    "Payroll cycle not found."
            });
        }

        if (!await CanViewCycleAsync(cycle))
            return Forbid();

        if (cycle.IsLocked)
        {
            return BadRequest(new
            {
                message =
                    "Locked payroll cannot be deleted."
            });
        }

        if (cycle.Status !=
                PayrollCycleStatus.Draft &&
            cycle.Status !=
                PayrollCycleStatus.Rejected)
        {
            return BadRequest(new
            {
                message =
                    $"Payroll cannot be deleted from {cycle.Status} status. Only Draft or Rejected payroll can be deleted."
            });
        }

        var adjustments =
            await _db.PayrollAdjustments
                .Where(x =>
                    x.PayrollCycleId ==
                    cycle.Id)
                .ToListAsync();

        if (adjustments.Count > 0)
        {
            _db.PayrollAdjustments.RemoveRange(
                adjustments);
        }

        if (cycle.PayrollRecords.Count > 0)
        {
            _db.PayrollRecords.RemoveRange(
                cycle.PayrollRecords);
        }

        _db.PayrollCycles.Remove(
            cycle);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message =
                $"{cycle.MonthYear} payroll deleted successfully."
        });
    }

    // ============================================================
    // PRIVATE: RECALCULATE PAYROLL
    // ============================================================

    private async Task RecalculatePayrollCycle(
        int cycleId)
    {
        var cycle =
            await _db.PayrollCycles
                .Include(x => x.PayrollRecords)
                .FirstOrDefaultAsync(x =>
                    x.Id == cycleId);

        if (cycle == null)
            return;

        var adjustments =
            await _db.PayrollAdjustments
                .AsNoTracking()
                .Where(x =>
                    x.PayrollCycleId ==
                        cycleId &&
                    x.Status ==
                        PayrollAdjustmentStatus.ApprovedByCompanyAdmin)
                .ToListAsync();

        foreach (var record in
                 cycle.PayrollRecords)
        {
            record.TotalBonus =
                adjustments
                    .Where(x =>
                        x.UserId ==
                            record.UserId &&
                        x.Type ==
                            PayrollAdjustmentType.Bonus)
                    .Sum(x => x.Amount);

            record.TotalDeduction =
                adjustments
                    .Where(x =>
                        x.UserId ==
                            record.UserId &&
                        x.Type ==
                            PayrollAdjustmentType.Deduction)
                    .Sum(x => x.Amount);

            record.NetSalary =
                record.BasicSalary
                + record.TotalBonus
                - record.TotalDeduction;
        }

        RecalculateCycle(cycle);

        await _db.SaveChangesAsync();
    }

    private static void RecalculateCycle(
        PayrollCycle cycle)
    {
        cycle.TotalBasicSalary =
            cycle.PayrollRecords.Sum(x =>
                x.BasicSalary);

        cycle.TotalBonus =
            cycle.PayrollRecords.Sum(x =>
                x.TotalBonus);

        cycle.TotalDeduction =
            cycle.PayrollRecords.Sum(x =>
                x.TotalDeduction);

        cycle.TotalNetSalary =
            cycle.PayrollRecords.Sum(x =>
                x.NetSalary);
    }

    // ============================================================
    // PRIVATE: PAYSLIP DTO BUILDER
    // ============================================================

    private async Task<List<PayslipDto>>
        BuildPayslipDtos(
            List<Estuscia.Domain.Entities.PayrollRecord> records,
            IEnumerable<dynamic>? suppliedUsers = null)
    {
        var userIds =
            records
                .Select(x => x.UserId)
                .Distinct()
                .ToList();

        var users =
            suppliedUsers?.ToList();

        if (users == null)
        {
            var loadedUsers =
                await _db.Users
                    .AsNoTracking()
                    .Where(x =>
                        userIds.Contains(x.Id))
                    .Select(x => new
                    {
                        x.Id,
                        x.EmployeeCode,
                        x.FullName,
                        x.Designation,
                        x.Department,
                        x.BranchId
                    })
                    .ToListAsync();

            users =
                loadedUsers
                    .Cast<dynamic>()
                    .ToList();
        }

        var adjustments =
            await _db.PayrollAdjustments
                .AsNoTracking()
                .Where(x =>
                    records
                        .Select(r =>
                            r.PayrollCycleId)
                        .Contains(
                            x.PayrollCycleId) &&
                    x.Status ==
                        PayrollAdjustmentStatus.ApprovedByCompanyAdmin)
                .Select(x => new
                {
                    x.Id,
                    x.PayrollCycleId,
                    x.UserId,
                    x.Type,
                    x.Amount,
                    x.Reason
                })
                .ToListAsync();

        var result =
            new List<PayslipDto>();

        foreach (var record in records)
        {
            dynamic? user =
                users.FirstOrDefault(x =>
                    x.Id ==
                    record.UserId);

            if (user == null)
                continue;

            var employeeAdjustments =
                adjustments
                    .Where(x =>
                        x.PayrollCycleId ==
                            record.PayrollCycleId &&
                        x.UserId ==
                            record.UserId)
                    .Select(x =>
                        new PayslipAdjustmentDto
                        {
                            Id =
                                x.Id,

                            Type =
                                x.Type.ToString(),

                            Amount =
                                x.Amount,

                            Reason =
                                x.Reason
                        })
                    .ToList();

            result.Add(new PayslipDto
            {
                Id =
                    record.Id,

                PayrollCycleId =
                    record.PayrollCycleId,

                UserId =
                    record.UserId,

                EmployeeCode =
                    user.EmployeeCode,

                EmployeeName =
                    $"{user.FullName}".Trim(),

                Designation =
                    user.Designation,

                Department =
                    user.Department,

                BranchId =
                    user.BranchId,

                MonthYear =
                    record.PayrollCycle?.MonthYear
                    ?? "",

                BasicSalary =
                    record.BasicSalary,

                TotalBonus =
                    record.TotalBonus,

                TotalDeduction =
                    record.TotalDeduction,

                NetSalary =
                    record.NetSalary,

                Status =
                    record.Status.ToString(),

                IsLocked =
                    record.IsLocked,

                PaidAtUtc =
                    record.PaidAtUtc,

                Adjustments =
                    employeeAdjustments
            });
        }

        return result;
    }

    // ============================================================
    // PRIVATE: EFFECTIVE BRANCH
    // ============================================================

    private async Task<int?> GetEffectiveBranchId(
        int? requestedBranchId)
    {
        // COMPANY ADMIN
        if (IsCompanyAdmin())
        {
            return requestedBranchId;
        }

        // SUPER ADMIN
        if (IsSuperAdmin())
        {
            return requestedBranchId;
        }

        // HR OPS
        if (IsHrOps())
        {
            var hrBranchId =
                await GetCurrentUserBranchId();

            if (hrBranchId.HasValue)
                return hrBranchId.Value;

            return requestedBranchId;
        }

        // BRANCH MANAGER
        if (IsBranchManager())
        {
            return await GetCurrentUserBranchId();
        }

        // EMPLOYEE
        return await GetCurrentUserBranchId();
    }

    // ============================================================
    // PRIVATE: CYCLE ACCESS
    // ============================================================

    private async Task<bool> CanViewCycleAsync(
        PayrollCycle cycle)
    {
        if (IsSuperAdmin() ||
            IsCompanyAdmin())
        {
            return true;
        }

        if (IsHrOps())
        {
            var hrBranchId =
                await GetCurrentUserBranchId();

            if (!hrBranchId.HasValue)
                return true;

            return await CycleContainsBranch(
                cycle.Id,
                hrBranchId.Value);
        }

        if (IsBranchManager())
        {
            var managerBranchId =
                await GetCurrentUserBranchId();

            if (!managerBranchId.HasValue)
                return false;

            return await CycleContainsBranch(
                cycle.Id,
                managerBranchId.Value);
        }

        return false;
    }

    // ============================================================
    // PRIVATE: PAYSLIP ACCESS
    // ============================================================

    private async Task<bool> CanViewPayslipAsync(
        Estuscia.Domain.Entities.PayrollRecord record)
    {
        if (IsSuperAdmin() ||
            IsCompanyAdmin())
        {
            return true;
        }

        var employee =
            await _db.Users
                .AsNoTracking()
                .Where(x =>
                    x.Id ==
                    record.UserId)
                .Select(x => new
                {
                    x.Id,
                    x.BranchId
                })
                .FirstOrDefaultAsync();

        if (employee == null)
            return false;

        if (IsHrOps())
        {
            var hrBranchId =
                await GetCurrentUserBranchId();

            if (!hrBranchId.HasValue)
                return true;

            return employee.BranchId ==
                   hrBranchId.Value;
        }

        if (IsBranchManager())
        {
            var managerBranchId =
                await GetCurrentUserBranchId();

            return managerBranchId.HasValue &&
                   employee.BranchId ==
                   managerBranchId.Value;
        }

        var currentUserId =
            GetCurrentUserId();

        return currentUserId.HasValue &&
               record.UserId ==
               currentUserId.Value;
    }

    // ============================================================
    // PRIVATE: CYCLE BRANCH CHECK
    // ============================================================

    private async Task<bool> CycleContainsBranch(
        int cycleId,
        int branchId)
    {
        return await _db.PayrollRecords
            .AsNoTracking()
            .Where(x =>
                x.PayrollCycleId ==
                cycleId)
            .Join(
                _db.Users.AsNoTracking(),
                record => record.UserId,
                user => user.Id,
                (record, user) =>
                    user.BranchId)
            .AnyAsync(x =>
                x == branchId);
    }

    // ============================================================
    // PRIVATE: PERMISSIONS
    // ============================================================

    private bool CanViewPayroll()
    {
        return IsSuperAdmin()
            || IsCompanyAdmin()
            || IsHrOps()
            || IsBranchManager()
            || IsEmployee();
    }

    private bool CanManagePayroll()
    {
        return IsSuperAdmin()
            || IsCompanyAdmin()
            || IsHrOps();
    }

    private bool CanSubmitPayroll()
    {
        return IsSuperAdmin()
            || IsHrOps()
            || IsCompanyAdmin();
    }

    private bool CanProcessPayment()
    {
        return IsSuperAdmin()
            || IsCompanyAdmin()
            || IsHrOps();
    }

    private bool CanViewAllPayslips()
    {
        return IsSuperAdmin()
            || IsCompanyAdmin()
            || IsHrOps()
            || IsBranchManager();
    }

    // ============================================================
    // PRIVATE: ROLES
    // ============================================================

    private bool IsEmployee()
    {
        return HasRole(
            "sales_staff",
            "developer",
            "support_staff",
            "knowledge_trainer");
    }

    private bool IsBranchManager()
    {
        return HasRole(
            "branch_manager");
    }

    private bool IsHrOps()
    {
        return HasRole(
            "hr_ops");
    }

    private bool IsCompanyAdmin()
    {
        return HasRole(
            "company_admin");
    }

    private bool IsSuperAdmin()
    {
        return HasRole(
            "super_admin");
    }

    private bool HasRole(
        params string[] roles)
    {
        var role =
            User.FindFirstValue(
                ClaimTypes.Role)
            ?? User.FindFirstValue("role")
            ?? User.FindFirstValue("Role");

        if (string.IsNullOrWhiteSpace(role))
            return false;

        return roles.Any(x =>
            string.Equals(
                x,
                role,
                StringComparison.OrdinalIgnoreCase));
    }

    // ============================================================
    // PRIVATE: CURRENT USER
    // ============================================================

    private int? GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("userId")
            ?? User.FindFirstValue("UserId");

        if (int.TryParse(value, out var id))
            return id;

        return null;
    }

    private async Task<int?> GetCurrentUserBranchId()
    {
        var userId =
            GetCurrentUserId();

        if (!userId.HasValue)
            return null;

        return await _db.Users
            .AsNoTracking()
            .Where(x =>
                x.Id == userId.Value)
            .Select(x =>
                x.BranchId)
            .FirstOrDefaultAsync();
    }
}


// =================================================================
// REQUEST DTOs
// =================================================================

public class GeneratePayrollRequest
{
    public int Year { get; set; }

    public int Month { get; set; }

    public int? BranchId { get; set; }
}


// -----------------------------------------------------------------
// Pay selected payroll employees
// -----------------------------------------------------------------

public class PayPayrollRequest
{
    public List<int> PayrollRecordIds { get; set; } = new();
}


// -----------------------------------------------------------------
// Reject payroll / adjustment
// -----------------------------------------------------------------

public class RejectPayrollRequest
{
    public string Reason { get; set; }
        = string.Empty;
}


// -----------------------------------------------------------------
// Create payroll adjustment
// -----------------------------------------------------------------

public class CreatePayrollAdjustmentRequest
{
    public int UserId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PayrollAdjustmentType Type { get; set; }

    public decimal Amount { get; set; }

    public string Reason { get; set; }
        = string.Empty;
}


// =================================================================
// RESPONSE DTOs
// =================================================================

public class PayrollCycleDto
{
    public int Id { get; set; }

    public int? TenantId { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    public string MonthYear { get; set; }
        = string.Empty;

    public string Status { get; set; }
        = string.Empty;

    public decimal TotalBasicSalary { get; set; }

    public decimal TotalBonus { get; set; }

    public decimal TotalDeduction { get; set; }

    public decimal TotalNetSalary { get; set; }

    public int? SubmittedByUserId { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    public int? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public int? RejectedByUserId { get; set; }

    public DateTime? RejectedAtUtc { get; set; }

    public string? RejectionReason { get; set; }

    public int? PaidByUserId { get; set; }

    public DateTime? PaidAtUtc { get; set; }

    public bool IsLocked { get; set; }

    public int EmployeeCount { get; set; }

    public int PaidEmployeeCount { get; set; }

    public int UnpaidEmployeeCount { get; set; }
}


public class PayrollCycleDetailsDto
    : PayrollCycleDto
{
    public List<PayrollRecordDto> PayrollRecords
    {
        get;
        set;
    } = new();
}


public class PayrollRecordDto
{
    public int Id { get; set; }

    public int PayrollCycleId { get; set; }

    public int UserId { get; set; }

    public string? EmployeeCode { get; set; }

    public string? EmployeeName { get; set; }

    public string? Designation { get; set; }

    public string? Department { get; set; }

    public int? BranchId { get; set; }

    public decimal BasicSalary { get; set; }

    public decimal TotalBonus { get; set; }

    public decimal TotalDeduction { get; set; }

    public decimal NetSalary { get; set; }

    public string Status { get; set; }
        = string.Empty;

    public string PaymentStatus { get; set; }
        = string.Empty;

    public bool IsLocked { get; set; }

    public int? SubmittedByUserId { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    public int? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public int? PaidByUserId { get; set; }

    public DateTime? PaidAtUtc { get; set; }
}


public class PayrollAdjustmentDto
{
    public int Id { get; set; }

    public int PayrollCycleId { get; set; }

    public int UserId { get; set; }

    public string? EmployeeCode { get; set; }

    public string? EmployeeName { get; set; }

    public string Type { get; set; }
        = string.Empty;

    public decimal Amount { get; set; }

    public string Reason { get; set; }
        = string.Empty;

    public string Status { get; set; }
        = string.Empty;

    public int? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public int? RejectedByUserId { get; set; }

    public DateTime? RejectedAtUtc { get; set; }

    public string? RejectionReason { get; set; }
}


public class PayslipDto
{
    public int Id { get; set; }

    public int PayrollCycleId { get; set; }

    public int UserId { get; set; }

    public string? EmployeeCode { get; set; }

    public string? EmployeeName { get; set; }

    public string? Designation { get; set; }

    public string? Department { get; set; }

    public int? BranchId { get; set; }

    public string MonthYear { get; set; }
        = string.Empty;

    public decimal BasicSalary { get; set; }

    public decimal TotalBonus { get; set; }

    public decimal TotalDeduction { get; set; }

    public decimal NetSalary { get; set; }

    public string Status { get; set; }
        = string.Empty;

    public bool IsLocked { get; set; }

    public DateTime? PaidAtUtc { get; set; }

    public List<PayslipAdjustmentDto> Adjustments
    {
        get;
        set;
    } = new();
}


public class PayslipAdjustmentDto
{
    public int Id { get; set; }

    public string Type { get; set; }
        = string.Empty;

    public decimal Amount { get; set; }

    public string Reason { get; set; }
        = string.Empty;
}