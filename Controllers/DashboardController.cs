using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using smart_hr_attendance_payroll_management.Common;
using smart_hr_attendance_payroll_management.Data;
using smart_hr_attendance_payroll_management.DTOs;
using smart_hr_attendance_payroll_management.Services;

namespace smart_hr_attendance_payroll_management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,HR,Manager")]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly DistributedCacheService _cache;

        public DashboardController(AppDbContext context, DistributedCacheService cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview([FromQuery] int? year, [FromQuery] int? month)
        {
            var now = DateTime.Now;
            var selectedYear = year ?? now.Year;
            var selectedMonth = month ?? now.Month;

            if (selectedMonth < 1 || selectedMonth > 12)
                return BadRequest("Month must be between 1 and 12.");

            if (selectedYear < 2000 || selectedYear > 2100)
                return BadRequest("Year is invalid.");

            var managerDepartmentId = await GetManagedDepartmentIdAsync();
            var cacheKey = $"dashboard:overview:{User.Identity?.Name}:{selectedYear}:{selectedMonth}:{managerDepartmentId}";
            var response = await _cache.GetOrCreateAsync(cacheKey, TimeSpan.FromMinutes(3), async () =>
            {
                var employeeQuery = _context.Employees.AsNoTracking().AsQueryable();
                if (managerDepartmentId.HasValue)
                    employeeQuery = employeeQuery.Where(e => e.DepartmentId == managerDepartmentId.Value);
                var employeeIds = await employeeQuery.Select(e => e.Id).ToListAsync();

                var totalEmployees = employeeIds.Count;
                var activeEmployees = await employeeQuery.CountAsync(e => e.IsActive);
                var inactiveEmployees = totalEmployees - activeEmployees;

                var totalDepartments = managerDepartmentId.HasValue
                    ? await _context.Departments.CountAsync(d => d.Id == managerDepartmentId.Value)
                    : await _context.Departments.CountAsync();
                var totalPositions = managerDepartmentId.HasValue
                    ? await _context.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id)).Select(e => e.PositionId).Distinct().CountAsync()
                    : await _context.Positions.CountAsync();

                var pendingLeaveRequests = await _context.LeaveRequests.CountAsync(l => l.Status == LeaveStatuses.Pending && employeeIds.Contains(l.EmployeeId));
                var monthlyAttendances = await _context.Attendances.Where(a => employeeIds.Contains(a.EmployeeId) && a.WorkDate.Year == selectedYear && a.WorkDate.Month == selectedMonth).ToListAsync();
                var monthlyPayrolls = await _context.Payrolls.Where(p => employeeIds.Contains(p.EmployeeId) && p.PayrollYear == selectedYear && p.PayrollMonth == selectedMonth).ToListAsync();

                return new DashboardOverviewResponse
                {
                    Year = selectedYear,
                    Month = selectedMonth,
                    TotalEmployees = totalEmployees,
                    ActiveEmployees = activeEmployees,
                    InactiveEmployees = inactiveEmployees,
                    TotalDepartments = totalDepartments,
                    TotalPositions = totalPositions,
                    PendingLeaveRequests = pendingLeaveRequests,
                    MonthlyAttendanceRecords = monthlyAttendances.Count,
                    MonthlyPayrollRecords = monthlyPayrolls.Count,
                    MonthlyPresentCount = monthlyAttendances.Count(a => a.Status == AttendanceStatuses.Present),
                    MonthlyLateCount = monthlyAttendances.Count(a => a.Status == AttendanceStatuses.Late),
                    MonthlyAbsentCount = monthlyAttendances.Count(a => a.Status == AttendanceStatuses.Absent),
                    MonthlyLeaveCount = monthlyAttendances.Count(a => a.Status == AttendanceStatuses.Leave),
                    MonthlyRemoteCount = monthlyAttendances.Count(a => a.Status == AttendanceStatuses.Remote),
                    MonthlyTotalNetSalary = monthlyPayrolls.Sum(p => p.NetSalary)
                };
            });

            return Ok(response);
        }

        [HttpGet("attendance-monthly")]
        public async Task<IActionResult> GetAttendanceMonthly([FromQuery] int year, [FromQuery] int month)
        {
            if (month < 1 || month > 12)
                return BadRequest("Month must be between 1 and 12.");

            if (year < 2000 || year > 2100)
                return BadRequest("Year is invalid.");

            var employeeIds = await GetScopedEmployeeIdsAsync();
            var attendances = await _context.Attendances
                .AsNoTracking()
                .Where(a => employeeIds.Contains(a.EmployeeId) && a.WorkDate.Year == year && a.WorkDate.Month == month)
                .ToListAsync();

            var response = new AttendanceMonthlyDashboardResponse
            {
                Year = year,
                Month = month,
                TotalAttendanceRecords = attendances.Count,
                PresentCount = attendances.Count(a => a.Status == AttendanceStatuses.Present),
                LateCount = attendances.Count(a => a.Status == AttendanceStatuses.Late),
                AbsentCount = attendances.Count(a => a.Status == AttendanceStatuses.Absent),
                LeaveCount = attendances.Count(a => a.Status == AttendanceStatuses.Leave),
                RemoteCount = attendances.Count(a => a.Status == AttendanceStatuses.Remote)
            };

            return Ok(response);
        }

        [HttpGet("payroll-monthly")]
        public async Task<IActionResult> GetPayrollMonthly([FromQuery] int year, [FromQuery] int month)
        {
            if (month < 1 || month > 12)
                return BadRequest("Month must be between 1 and 12.");

            if (year < 2000 || year > 2100)
                return BadRequest("Year is invalid.");

            var employeeIds = await GetScopedEmployeeIdsAsync();
            var payrolls = await _context.Payrolls
                .AsNoTracking()
                .Where(p => employeeIds.Contains(p.EmployeeId) && p.PayrollYear == year && p.PayrollMonth == month)
                .ToListAsync();

            var totalPayrollRecords = payrolls.Count;
            var totalBaseSalary = payrolls.Sum(p => p.BaseSalary);
            var totalBonus = payrolls.Sum(p => p.Bonus);
            var totalDeduction = payrolls.Sum(p => p.Deduction);
            var totalNetSalary = payrolls.Sum(p => p.NetSalary);
            var averageNetSalary = totalPayrollRecords > 0
                ? Math.Round(totalNetSalary / totalPayrollRecords, 2)
                : 0;

            var response = new PayrollMonthlyDashboardResponse
            {
                Year = year,
                Month = month,
                TotalPayrollRecords = totalPayrollRecords,
                TotalBaseSalary = totalBaseSalary,
                TotalBonus = totalBonus,
                TotalDeduction = totalDeduction,
                TotalNetSalary = totalNetSalary,
                AverageNetSalary = averageNetSalary
            };

            return Ok(response);
        }

        [HttpGet("leave-monthly")]
        public async Task<IActionResult> GetLeaveMonthly([FromQuery] int year, [FromQuery] int month)
        {
            if (month < 1 || month > 12)
                return BadRequest("Month must be between 1 and 12.");

            if (year < 2000 || year > 2100)
                return BadRequest("Year is invalid.");

            var employeeIds = await GetScopedEmployeeIdsAsync();
            var leaveRequests = await _context.LeaveRequests
                .AsNoTracking()
                .Where(l => employeeIds.Contains(l.EmployeeId) && (
                    (l.StartDate.Year == year && l.StartDate.Month == month) ||
                    (l.EndDate.Year == year && l.EndDate.Month == month) ||
                    (l.StartDate < new DateTime(year, month, 1) &&
                     l.EndDate > new DateTime(year, month, 1).AddMonths(1).AddDays(-1))))
                .ToListAsync();

            var response = new LeaveMonthlyDashboardResponse
            {
                Year = year,
                Month = month,
                TotalRequests = leaveRequests.Count,
                PendingRequests = leaveRequests.Count(l => l.Status == LeaveStatuses.Pending),
                ApprovedRequests = leaveRequests.Count(l => l.Status == LeaveStatuses.Approved),
                RejectedRequests = leaveRequests.Count(l => l.Status == LeaveStatuses.Rejected),
                CancelledRequests = leaveRequests.Count(l => l.Status == LeaveStatuses.Cancelled),

                ApprovedLeaveDays = leaveRequests
                    .Where(l => l.Status == LeaveStatuses.Approved)
                    .Sum(l => CalculateDaysWithinMonth(l.StartDate, l.EndDate, year, month)),

                AnnualLeaveRequests = leaveRequests.Count(l => l.LeaveType == LeaveTypes.AnnualLeave),
                SickLeaveRequests = leaveRequests.Count(l => l.LeaveType == LeaveTypes.SickLeave),
                UnpaidLeaveRequests = leaveRequests.Count(l => l.LeaveType == LeaveTypes.UnpaidLeave),
                OtherLeaveRequests = leaveRequests.Count(l => l.LeaveType == LeaveTypes.Other)
            };

            return Ok(response);
        }

        [HttpGet("department-headcount")]
        public async Task<IActionResult> GetDepartmentHeadcount()
        {
            var managerDepartmentId = await GetManagedDepartmentIdAsync();
            var departmentsQuery = _context.Departments.AsNoTracking().AsQueryable();
            if (managerDepartmentId.HasValue)
                departmentsQuery = departmentsQuery.Where(d => d.Id == managerDepartmentId.Value);

            var departments = await departmentsQuery
                .Select(d => new DepartmentHeadcountResponse
                {
                    DepartmentId = d.Id,
                    DepartmentCode = d.DepartmentCode,
                    DepartmentName = d.DepartmentName,
                    EmployeeCount = d.Employees.Count,
                    ActiveEmployeeCount = d.Employees.Count(e => e.IsActive),
                    InactiveEmployeeCount = d.Employees.Count(e => !e.IsActive)
                })
                .OrderByDescending(d => d.EmployeeCount)
                .ThenBy(d => d.DepartmentName)
                .ToListAsync();

            return Ok(departments);
        }

        [HttpGet("recent-leave-requests")]
        public async Task<IActionResult> GetRecentLeaveRequests([FromQuery] int take = 5)
        {
            if (take <= 0 || take > 20)
                return BadRequest("Take must be between 1 and 20.");

            var employeeIds = await GetScopedEmployeeIdsAsync();
            var leaveRequests = await _context.LeaveRequests
                .AsNoTracking()
                .Include(l => l.Employee)
                .Include(l => l.ApprovedByUser)
                .Where(l => employeeIds.Contains(l.EmployeeId))
                .OrderByDescending(l => l.CreatedAt)
                .Take(take)
                .Select(l => new RecentLeaveRequestResponse
                {
                    Id = l.Id,
                    EmployeeId = l.EmployeeId,
                    EmployeeCode = l.Employee != null ? l.Employee.EmployeeCode : string.Empty,
                    FullName = l.Employee != null ? l.Employee.FullName : string.Empty,
                    LeaveType = l.LeaveType,
                    Status = l.Status,
                    StartDate = l.StartDate,
                    EndDate = l.EndDate,
                    TotalDays = l.TotalDays,
                    Reason = l.Reason,
                    CreatedAt = l.CreatedAt,
                    ApprovedAt = l.ApprovedAt,
                    ApprovedByUsername = l.ApprovedByUser != null ? l.ApprovedByUser.Username : null
                })
                .ToListAsync();

            return Ok(leaveRequests);
        }

        [HttpGet("recent-payrolls")]
        public async Task<IActionResult> GetRecentPayrolls([FromQuery] int take = 5)
        {
            if (take <= 0 || take > 20)
                return BadRequest("Take must be between 1 and 20.");

            var employeeIds = await GetScopedEmployeeIdsAsync();
            var payrolls = await _context.Payrolls
                .AsNoTracking()
                .Include(p => p.Employee)
                .Where(p => employeeIds.Contains(p.EmployeeId))
                .OrderByDescending(p => p.GeneratedAt)
                .Take(take)
                .Select(p => new RecentPayrollResponse
                {
                    Id = p.Id,
                    EmployeeId = p.EmployeeId,
                    EmployeeCode = p.Employee != null ? p.Employee.EmployeeCode : string.Empty,
                    FullName = p.Employee != null ? p.Employee.FullName : string.Empty,
                    PayrollMonth = p.PayrollMonth,
                    PayrollYear = p.PayrollYear,
                    BaseSalary = p.BaseSalary,
                    Bonus = p.Bonus,
                    Deduction = p.Deduction,
                    NetSalary = p.NetSalary,
                    GeneratedAt = p.GeneratedAt
                })
                .ToListAsync();

            return Ok(payrolls);
        }

        [HttpGet("recent-attendances")]
        public async Task<IActionResult> GetRecentAttendances([FromQuery] int take = 5)
        {
            if (take <= 0 || take > 20)
                return BadRequest("Take must be between 1 and 20.");

            var employeeIds = await GetScopedEmployeeIdsAsync();
            var attendances = await _context.Attendances
                .AsNoTracking()
                .Include(a => a.Employee)
                    .ThenInclude(e => e.Department)
                .Include(a => a.Employee)
                    .ThenInclude(e => e.Position)
                .Where(a => employeeIds.Contains(a.EmployeeId))
                .OrderByDescending(a => a.WorkDate)
                .ThenByDescending(a => a.Id)
                .Take(take)
                .Select(a => new RecentAttendanceResponse
                {
                    Id = a.Id,
                    EmployeeId = a.EmployeeId,
                    EmployeeCode = a.Employee != null ? a.Employee.EmployeeCode : string.Empty,
                    FullName = a.Employee != null ? a.Employee.FullName : string.Empty,
                    DepartmentName = a.Employee != null && a.Employee.Department != null
                        ? a.Employee.Department.DepartmentName
                        : string.Empty,
                    PositionName = a.Employee != null && a.Employee.Position != null
                        ? a.Employee.Position.PositionName
                        : string.Empty,
                    WorkDate = a.WorkDate,
                    CheckInTime = a.CheckInTime,
                    CheckOutTime = a.CheckOutTime,
                    Status = a.Status,
                    Note = a.Note,
                    SourceType = a.SourceType,
                    SourceReferenceId = a.SourceReferenceId
                })
                .ToListAsync();

            return Ok(attendances);
        }

        [HttpGet("employee-status-summary")]
        public async Task<IActionResult> GetEmployeeStatusSummary()
        {
            var employeeIds = await GetScopedEmployeeIdsAsync();
            var totalEmployees = employeeIds.Count;
            var activeEmployees = await _context.Employees.CountAsync(e => employeeIds.Contains(e.Id) && e.IsActive);
            var inactiveEmployees = totalEmployees - activeEmployees;

            decimal activePercentage = 0;
            decimal inactivePercentage = 0;

            if (totalEmployees > 0)
            {
                activePercentage = Math.Round((decimal)activeEmployees * 100 / totalEmployees, 2);
                inactivePercentage = Math.Round((decimal)inactiveEmployees * 100 / totalEmployees, 2);
            }

            var response = new EmployeeStatusSummaryResponse
            {
                TotalEmployees = totalEmployees,
                ActiveEmployees = activeEmployees,
                InactiveEmployees = inactiveEmployees,
                ActivePercentage = activePercentage,
                InactivePercentage = inactivePercentage
            };

            return Ok(response);
        }


        private async Task<int?> GetManagedDepartmentIdAsync()
        {
            if (!User.IsInRole(UserRoles.Manager))
                return null;

            var employeeIdClaim = User.FindFirst("employeeId")?.Value;
            if (!int.TryParse(employeeIdClaim, out var employeeId))
                return null;

            return await _context.Employees.AsNoTracking()
                .Where(e => e.Id == employeeId)
                .Select(e => (int?)e.DepartmentId)
                .FirstOrDefaultAsync();
        }

        private async Task<List<int>> GetScopedEmployeeIdsAsync()
        {
            var managerDepartmentId = await GetManagedDepartmentIdAsync();
            var employeeQuery = _context.Employees.AsNoTracking().AsQueryable();
            if (managerDepartmentId.HasValue)
                employeeQuery = employeeQuery.Where(e => e.DepartmentId == managerDepartmentId.Value);
            return await employeeQuery.Select(e => e.Id).ToListAsync();
        }

        private static int CalculateDaysWithinMonth(DateTime startDate, DateTime endDate, int year, int month)
        {
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var effectiveStart = startDate.Date > monthStart ? startDate.Date : monthStart;
            var effectiveEnd = endDate.Date < monthEnd ? endDate.Date : monthEnd;

            if (effectiveEnd < effectiveStart)
                return 0;

            return (effectiveEnd - effectiveStart).Days + 1;
        }
    }
}