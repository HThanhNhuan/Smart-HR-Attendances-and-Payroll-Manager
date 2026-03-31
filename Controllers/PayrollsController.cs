using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using smart_hr_attendance_payroll_management.Common;
using smart_hr_attendance_payroll_management.Data;
using smart_hr_attendance_payroll_management.DTOs;
using smart_hr_attendance_payroll_management.Entities;
using System.Security.Claims;
using System.Text.Json;

namespace smart_hr_attendance_payroll_management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PayrollsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public PayrollsController(AppDbContext context) { _context = context; }

        [HttpGet]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetAll([FromQuery] PayrollQueryRequest query)
        {
            var payrollQuery = BuildQuery();
            if (query.EmployeeId.HasValue) payrollQuery = payrollQuery.Where(p => p.EmployeeId == query.EmployeeId.Value);
            if (query.Month.HasValue) payrollQuery = payrollQuery.Where(p => p.PayrollMonth == query.Month.Value);
            if (query.Year.HasValue) payrollQuery = payrollQuery.Where(p => p.PayrollYear == query.Year.Value);
            var payrolls = await payrollQuery.OrderByDescending(p => p.PayrollYear).ThenByDescending(p => p.PayrollMonth).ThenBy(p => p.EmployeeId).ToListAsync();
            return Ok(payrolls.Select(MapToResponse));
        }

        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetById(int id)
        {
            var payroll = await BuildQuery().FirstOrDefaultAsync(p => p.Id == id);
            return payroll == null ? NotFound() : Ok(MapToResponse(payroll));
        }

        [HttpGet("{id:int}/history")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetHistory(int id)
        {
            var payrollExists = await _context.Payrolls.AsNoTracking().AnyAsync(p => p.Id == id);
            if (!payrollExists) return NotFound();
            var history = await _context.PayrollAuditLogs.AsNoTracking().Include(x => x.PerformedByUser).Where(x => x.PayrollId == id).OrderByDescending(x => x.CreatedAt).ToListAsync();
            return Ok(history.Select(MapAuditResponse));
        }

        [HttpGet("history/recent")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetRecentHistory([FromQuery] int take = 8)
        {
            if (take <= 0 || take > 30) return BadRequest("Take must be between 1 and 30.");
            var history = await _context.PayrollAuditLogs.AsNoTracking().Include(x => x.PerformedByUser).OrderByDescending(x => x.CreatedAt).Take(take).ToListAsync();
            return Ok(history.Select(MapAuditResponse));
        }

        [HttpPost("generate")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> Generate(GeneratePayrollRequest request)
        {
            var employee = await _context.Employees.Include(e => e.Department).Include(e => e.Position).FirstOrDefaultAsync(e => e.Id == request.EmployeeId);
            if (employee == null) return BadRequest("EmployeeId does not exist.");
            var payrollExists = await _context.Payrolls.AnyAsync(p => p.EmployeeId == request.EmployeeId && p.PayrollMonth == request.Month && p.PayrollYear == request.Year);
            if (payrollExists) return BadRequest("Payroll for this employee and month/year already exists.");
            var payroll = await BuildPayrollEntityAsync(employee, request.Month, request.Year, request.Bonus, request.Deduction);
            _context.Payrolls.Add(payroll);
            await _context.SaveChangesAsync();
            payroll.Employee = employee;
            await MarkOvertimeAppliedAsync(employee.Id, request.Month, request.Year);
            await AppendPayrollAuditLogAsync(payroll, "Generated", "Payroll generated for an individual employee.", GetCurrentUserId());
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = payroll.Id }, MapToResponse(payroll));
        }

        [HttpPost("generate-all")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> GenerateAll(GenerateAllPayrollsRequest request)
        {
            var employees = await _context.Employees.Include(e => e.Department).Include(e => e.Position).Where(e => e.IsActive).OrderBy(e => e.Id).ToListAsync();
            var actorId = GetCurrentUserId();
            var result = new GenerateAllPayrollsResponse { Month = request.Month, Year = request.Year, TotalEmployees = employees.Count };
            foreach (var employee in employees)
            {
                var existingPayroll = await _context.Payrolls.Include(p => p.Employee).ThenInclude(e => e.Department).Include(p => p.Employee).ThenInclude(e => e.Position).FirstOrDefaultAsync(p => p.EmployeeId == employee.Id && p.PayrollMonth == request.Month && p.PayrollYear == request.Year);
                if (existingPayroll != null && !request.OverwriteExisting)
                {
                    result.SkippedCount++;
                    result.Payrolls.Add(MapToResponse(existingPayroll));
                    await AppendPayrollAuditLogAsync(existingPayroll, "Skipped", "Generate-all skipped an existing payroll because overwriteExisting=false.", actorId);
                    await _context.SaveChangesAsync();
                    continue;
                }

                var generatedPayroll = await BuildPayrollEntityAsync(employee, request.Month, request.Year, 0, 0);
                if (existingPayroll == null)
                {
                    _context.Payrolls.Add(generatedPayroll);
                    await _context.SaveChangesAsync();
                    generatedPayroll.Employee = employee;
                    await MarkOvertimeAppliedAsync(employee.Id, request.Month, request.Year);
                    result.CreatedCount++;
                    result.Payrolls.Add(MapToResponse(generatedPayroll));
                    await AppendPayrollAuditLogAsync(generatedPayroll, "Generated", "Generate-all created a new payroll record.", actorId);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    existingPayroll.BaseSalary = generatedPayroll.BaseSalary;
                    existingPayroll.DailySalary = generatedPayroll.DailySalary;
                    existingPayroll.PresentDays = generatedPayroll.PresentDays;
                    existingPayroll.LateDays = generatedPayroll.LateDays;
                    existingPayroll.RemoteDays = generatedPayroll.RemoteDays;
                    existingPayroll.AbsentDays = generatedPayroll.AbsentDays;
                    existingPayroll.LeaveDays = generatedPayroll.LeaveDays;
                    existingPayroll.EffectiveWorkingDays = generatedPayroll.EffectiveWorkingDays;
                    existingPayroll.PaidLeaveDays = generatedPayroll.PaidLeaveDays;
                    existingPayroll.UnpaidLeaveDays = generatedPayroll.UnpaidLeaveDays;
                    existingPayroll.OvertimeHours = generatedPayroll.OvertimeHours;
                    existingPayroll.ApprovedOvertimeRequests = generatedPayroll.ApprovedOvertimeRequests;
                    existingPayroll.OvertimePay = generatedPayroll.OvertimePay;
                    existingPayroll.NetSalary = Math.Round(((generatedPayroll.EffectiveWorkingDays + generatedPayroll.PaidLeaveDays) * generatedPayroll.DailySalary) + generatedPayroll.OvertimePay + existingPayroll.Bonus - existingPayroll.Deduction, 2);
                    existingPayroll.GeneratedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                    await MarkOvertimeAppliedAsync(employee.Id, request.Month, request.Year);
                    existingPayroll.Employee = employee;
                    result.UpdatedCount++;
                    result.Payrolls.Add(MapToResponse(existingPayroll));
                    await AppendPayrollAuditLogAsync(existingPayroll, "Regenerated", "Generate-all refreshed payroll metrics while preserving bonus and deduction values.", actorId);
                    await _context.SaveChangesAsync();
                }
            }
            return Ok(result);
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> Update(int id, UpdatePayrollRequest request)
        {
            var payroll = await _context.Payrolls.Include(p => p.Employee).ThenInclude(e => e.Department).Include(p => p.Employee).ThenInclude(e => e.Position).FirstOrDefaultAsync(p => p.Id == id);
            if (payroll == null) return NotFound();
            payroll.Bonus = request.Bonus;
            payroll.Deduction = request.Deduction;
            payroll.NetSalary = Math.Round(((payroll.EffectiveWorkingDays + payroll.PaidLeaveDays) * payroll.DailySalary) + payroll.OvertimePay + payroll.Bonus - payroll.Deduction, 2);
            await _context.SaveChangesAsync();
            await AppendPayrollAuditLogAsync(payroll, "Adjusted", "Payroll bonus and deduction were updated from the UI.", GetCurrentUserId());
            await _context.SaveChangesAsync();
            return Ok(MapToResponse(payroll));
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var payroll = await _context.Payrolls.Include(p => p.Employee).FirstOrDefaultAsync(p => p.Id == id);
            if (payroll == null) return NotFound();
            await AppendPayrollAuditLogAsync(payroll, "Deleted", "Admin deleted the payroll record.", GetCurrentUserId());
            await _context.SaveChangesAsync();
            _context.Payrolls.Remove(payroll);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("my-payrolls")]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> GetMyPayrolls([FromQuery] int? month, [FromQuery] int? year)
        {
            var employeeId = GetCurrentEmployeeId();
            if (!employeeId.HasValue)
                return Forbid();

            var query = BuildQuery().Where(p => p.EmployeeId == employeeId.Value);

            if (month.HasValue)
                query = query.Where(p => p.PayrollMonth == month.Value);

            if (year.HasValue)
                query = query.Where(p => p.PayrollYear == year.Value);

            var payrolls = await query
                .OrderByDescending(p => p.PayrollYear)
                .ThenByDescending(p => p.PayrollMonth)
                .ToListAsync();

            return Ok(payrolls.Select(MapToResponse));
        }

        private IQueryable<Payroll> BuildQuery() => _context.Payrolls.AsNoTracking().Include(p => p.Employee).ThenInclude(e => e.Department).Include(p => p.Employee).ThenInclude(e => e.Position);

        private async Task<Payroll> BuildPayrollEntityAsync(Employee employee, int month, int year, decimal bonus, decimal deduction)
        {
            var attendances = await _context.Attendances.Where(a => a.EmployeeId == employee.Id && a.WorkDate.Month == month && a.WorkDate.Year == year).ToListAsync();
            var approvedLeaves = await _context.LeaveRequests.Where(l => l.EmployeeId == employee.Id && l.Status == LeaveStatuses.Approved && ((l.StartDate.Year == year && l.StartDate.Month == month) || (l.EndDate.Year == year && l.EndDate.Month == month) || (l.StartDate < new DateTime(year, month, 1) && l.EndDate >= new DateTime(year, month, 1)))).ToListAsync();
            var approvedOvertime = await _context.OvertimeRequests.Where(o => o.EmployeeId == employee.Id && o.Status == OvertimeStatuses.Approved && o.WorkDate.Month == month && o.WorkDate.Year == year).ToListAsync();

            var presentDays = attendances.Count(a => a.Status == AttendanceStatuses.Present);
            var lateDays = attendances.Count(a => a.Status == AttendanceStatuses.Late);
            var remoteDays = attendances.Count(a => a.Status == AttendanceStatuses.Remote);
            var absentDays = attendances.Count(a => a.Status == AttendanceStatuses.Absent);
            var leaveDays = attendances.Count(a => a.Status == AttendanceStatuses.Leave);
            var effectiveWorkingDays = presentDays + lateDays + remoteDays;
            var paidLeaveDays = approvedLeaves.Where(l => l.LeaveType == LeaveTypes.AnnualLeave || l.LeaveType == LeaveTypes.SickLeave).Sum(l => (decimal)CalculateDaysWithinMonth(l.StartDate, l.EndDate, year, month));
            var unpaidLeaveDays = approvedLeaves.Where(l => l.LeaveType == LeaveTypes.UnpaidLeave || l.LeaveType == LeaveTypes.Other).Sum(l => (decimal)CalculateDaysWithinMonth(l.StartDate, l.EndDate, year, month));
            var overtimeHours = approvedOvertime.Sum(o => o.Hours);
            var dailySalary = Math.Round(employee.BaseSalary / 26m, 2);
            var hourlyRate = Math.Round(dailySalary / 8m, 2);
            var overtimePay = Math.Round(approvedOvertime.Sum(o => o.Hours * hourlyRate * GetOvertimeRate(o.WorkDate)), 2);

            return new Payroll
            {
                EmployeeId = employee.Id,
                PayrollMonth = month,
                PayrollYear = year,
                BaseSalary = employee.BaseSalary,
                DailySalary = dailySalary,
                PresentDays = presentDays,
                LateDays = lateDays,
                RemoteDays = remoteDays,
                AbsentDays = absentDays,
                LeaveDays = leaveDays,
                EffectiveWorkingDays = effectiveWorkingDays,
                PaidLeaveDays = paidLeaveDays,
                UnpaidLeaveDays = unpaidLeaveDays,
                OvertimeHours = overtimeHours,
                ApprovedOvertimeRequests = approvedOvertime.Count,
                Bonus = bonus,
                Deduction = deduction,
                OvertimePay = overtimePay,
                NetSalary = Math.Round(((effectiveWorkingDays + paidLeaveDays) * dailySalary) + overtimePay + bonus - deduction, 2),
                GeneratedAt = DateTime.Now
            };
        }

        private Task AppendPayrollAuditLogAsync(Payroll payroll, string actionType, string? note, int? performedByUserId)
        {
            var snapshot = JsonSerializer.Serialize(new { payroll.Id, payroll.EmployeeId, payroll.PayrollMonth, payroll.PayrollYear, payroll.BaseSalary, payroll.DailySalary, payroll.PresentDays, payroll.LateDays, payroll.RemoteDays, payroll.AbsentDays, payroll.LeaveDays, payroll.EffectiveWorkingDays, payroll.PaidLeaveDays, payroll.UnpaidLeaveDays, payroll.OvertimeHours, payroll.ApprovedOvertimeRequests, payroll.OvertimePay, payroll.Bonus, payroll.Deduction, payroll.NetSalary, payroll.GeneratedAt });
            _context.PayrollAuditLogs.Add(new PayrollAuditLog { PayrollId = payroll.Id, EmployeeId = payroll.EmployeeId, EmployeeCode = payroll.Employee?.EmployeeCode ?? string.Empty, EmployeeFullName = payroll.Employee?.FullName ?? string.Empty, PayrollMonth = payroll.PayrollMonth, PayrollYear = payroll.PayrollYear, BaseSalary = payroll.BaseSalary, Bonus = payroll.Bonus, Deduction = payroll.Deduction, NetSalary = payroll.NetSalary, PerformedByUserId = performedByUserId, ActionType = actionType, Note = note, SnapshotJson = snapshot, CreatedAt = DateTime.Now });
            return Task.CompletedTask;
        }

        private static PayrollResponse MapToResponse(Payroll payroll) => new PayrollResponse
        {
            Id = payroll.Id,
            EmployeeId = payroll.EmployeeId,
            EmployeeCode = payroll.Employee?.EmployeeCode ?? string.Empty,
            FullName = payroll.Employee?.FullName ?? string.Empty,
            DepartmentId = payroll.Employee?.DepartmentId ?? 0,
            DepartmentName = payroll.Employee?.Department?.DepartmentName ?? string.Empty,
            PositionId = payroll.Employee?.PositionId ?? 0,
            PositionName = payroll.Employee?.Position?.PositionName ?? string.Empty,
            PayrollMonth = payroll.PayrollMonth,
            PayrollYear = payroll.PayrollYear,
            BaseSalary = payroll.BaseSalary,
            DailySalary = payroll.DailySalary,
            PresentDays = payroll.PresentDays,
            LateDays = payroll.LateDays,
            RemoteDays = payroll.RemoteDays,
            AbsentDays = payroll.AbsentDays,
            LeaveDays = payroll.LeaveDays,
            EffectiveWorkingDays = payroll.EffectiveWorkingDays,
            PaidLeaveDays = payroll.PaidLeaveDays,
            UnpaidLeaveDays = payroll.UnpaidLeaveDays,
            OvertimeHours = payroll.OvertimeHours,
            ApprovedOvertimeRequests = payroll.ApprovedOvertimeRequests,
            Bonus = payroll.Bonus,
            Deduction = payroll.Deduction,
            OvertimePay = payroll.OvertimePay,
            NetSalary = payroll.NetSalary,
            GeneratedAt = payroll.GeneratedAt
        };

        private static PayrollAuditLogResponse MapAuditResponse(PayrollAuditLog item) => new PayrollAuditLogResponse
        {
            Id = item.Id,
            PayrollId = item.PayrollId,
            EmployeeId = item.EmployeeId,
            EmployeeCode = item.EmployeeCode,
            EmployeeFullName = item.EmployeeFullName,
            PayrollMonth = item.PayrollMonth,
            PayrollYear = item.PayrollYear,
            BaseSalary = item.BaseSalary,
            Bonus = item.Bonus,
            Deduction = item.Deduction,
            NetSalary = item.NetSalary,
            ActionType = item.ActionType,
            Note = item.Note,
            SnapshotJson = item.SnapshotJson,
            PerformedByUserId = item.PerformedByUserId,
            PerformedByUsername = item.PerformedByUser?.Username,
            CreatedAt = item.CreatedAt
        };

        private async Task MarkOvertimeAppliedAsync(int employeeId, int month, int year)
        {
            var items = await _context.OvertimeRequests.Where(o => o.EmployeeId == employeeId && o.Status == OvertimeStatuses.Approved && o.WorkDate.Month == month && o.WorkDate.Year == year).ToListAsync();
            foreach (var item in items)
            {
                item.AppliedToPayroll = true;
            }
            await _context.SaveChangesAsync();
        }

        private static int CalculateDaysWithinMonth(DateTime startDate, DateTime endDate, int year, int month)
        {
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var effectiveStart = startDate.Date > monthStart ? startDate.Date : monthStart;
            var effectiveEnd = endDate.Date < monthEnd ? endDate.Date : monthEnd;
            return effectiveEnd < effectiveStart ? 0 : (effectiveEnd - effectiveStart).Days + 1;
        }

        private static decimal GetOvertimeRate(DateTime workDate)
            => workDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 2.0m : 1.5m;

        private int? GetCurrentUserId() => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : null;

        private int? GetCurrentEmployeeId()
            => int.TryParse(User.FindFirst("employeeId")?.Value, out var employeeId)
                ? employeeId
                : null;
    }
}
