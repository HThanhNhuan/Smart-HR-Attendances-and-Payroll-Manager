using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using smart_hr_attendance_payroll_management.Common;
using smart_hr_attendance_payroll_management.Data;
using smart_hr_attendance_payroll_management.Services;
using System.Globalization;
using System.Text;

namespace smart_hr_attendance_payroll_management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,HR,Manager")]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly DistributedCacheService _cache;

        public ReportsController(AppDbContext context, DistributedCacheService cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpGet("monthly-summary/export")]
        public async Task<IActionResult> ExportMonthlySummary([FromQuery] int year, [FromQuery] int month, [FromQuery] string? format = "csv")
        {
            if (month is < 1 or > 12) return BadRequest("Month must be between 1 and 12.");
            if (year is < 2000 or > 2100) return BadRequest("Year is invalid.");

            var employeeIds = await GetScopedEmployeeIdsAsync();
            var totalEmployees = employeeIds.Count;
            var activeEmployees = await _context.Employees.AsNoTracking().CountAsync(e => employeeIds.Contains(e.Id) && e.IsActive);
            var inactiveEmployees = totalEmployees - activeEmployees;
            var pendingLeaveRequests = await _context.LeaveRequests.AsNoTracking().CountAsync(l => employeeIds.Contains(l.EmployeeId) && l.Status == LeaveStatuses.Pending);
            var monthlyAttendances = await _context.Attendances.AsNoTracking().Where(a => employeeIds.Contains(a.EmployeeId) && a.WorkDate.Year == year && a.WorkDate.Month == month).ToListAsync();
            var monthlyPayrolls = await _context.Payrolls.AsNoTracking().Where(p => employeeIds.Contains(p.EmployeeId) && p.PayrollYear == year && p.PayrollMonth == month).ToListAsync();
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var monthlyLeaves = await _context.LeaveRequests.AsNoTracking().Where(l => employeeIds.Contains(l.EmployeeId) && l.EndDate >= monthStart && l.StartDate <= monthEnd).ToListAsync();

            var rows = new List<string[]>
            {
                new[] { "Category", "Metric", "Value" },
                new[] { "Workforce", "Total Employees", totalEmployees.ToString() },
                new[] { "Workforce", "Active Employees", activeEmployees.ToString() },
                new[] { "Workforce", "Inactive Employees", inactiveEmployees.ToString() },
                new[] { "Leave", "Pending Leave Requests", pendingLeaveRequests.ToString() },
                new[] { "Attendance", "Monthly Attendance Records", monthlyAttendances.Count.ToString() },
                new[] { "Attendance", "Present", monthlyAttendances.Count(a => a.Status == AttendanceStatuses.Present).ToString() },
                new[] { "Attendance", "Late", monthlyAttendances.Count(a => a.Status == AttendanceStatuses.Late).ToString() },
                new[] { "Attendance", "Absent", monthlyAttendances.Count(a => a.Status == AttendanceStatuses.Absent).ToString() },
                new[] { "Attendance", "Leave", monthlyAttendances.Count(a => a.Status == AttendanceStatuses.Leave).ToString() },
                new[] { "Attendance", "Remote", monthlyAttendances.Count(a => a.Status == AttendanceStatuses.Remote).ToString() },
                new[] { "Payroll", "Payroll Records", monthlyPayrolls.Count.ToString() },
                new[] { "Payroll", "Total Bonus", monthlyPayrolls.Sum(p => p.Bonus).ToString("0.##", CultureInfo.InvariantCulture) },
                new[] { "Payroll", "Total Deduction", monthlyPayrolls.Sum(p => p.Deduction).ToString("0.##", CultureInfo.InvariantCulture) },
                new[] { "Payroll", "Total Net Salary", monthlyPayrolls.Sum(p => p.NetSalary).ToString("0.##", CultureInfo.InvariantCulture) },
                new[] { "Leave", "Total Requests In Month", monthlyLeaves.Count.ToString() },
                new[] { "Leave", "Approved Requests", monthlyLeaves.Count(l => l.Status == LeaveStatuses.Approved).ToString() },
                new[] { "Leave", "Rejected Requests", monthlyLeaves.Count(l => l.Status == LeaveStatuses.Rejected).ToString() },
                new[] { "Leave", "Cancelled Requests", monthlyLeaves.Count(l => l.Status == LeaveStatuses.Cancelled).ToString() }
            };

            return BuildReportFile(rows, "Monthly Summary", $"Operational summary for {month:00}/{year}", $"monthly-summary-{year}-{month:00}", format, "Summary");
        }

        [HttpGet("attendances/export")]
        public async Task<IActionResult> ExportAttendances([FromQuery] string? status, [FromQuery] int? employeeId, [FromQuery] int? month, [FromQuery] int? year, [FromQuery] string? search, [FromQuery] string? format = "csv")
        {
            var scopedEmployeeIds = await GetScopedEmployeeIdsAsync();
            var query = _context.Attendances.AsNoTracking()
                .Include(a => a.Employee).ThenInclude(e => e.Department)
                .Include(a => a.Employee).ThenInclude(e => e.Position)
                .Where(a => scopedEmployeeIds.Contains(a.EmployeeId))
                .AsQueryable();

            if (employeeId.HasValue) query = query.Where(a => a.EmployeeId == employeeId.Value);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(a => a.Status == AttendanceStatuses.Normalize(status));
            if (month.HasValue) query = query.Where(a => a.WorkDate.Month == month.Value);
            if (year.HasValue) query = query.Where(a => a.WorkDate.Year == year.Value);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalized = search.Trim().ToLower();
                query = query.Where(a =>
                    (a.Employee != null && a.Employee.FullName.ToLower().Contains(normalized)) ||
                    (a.Employee != null && a.Employee.EmployeeCode.ToLower().Contains(normalized)) ||
                    (a.Employee != null && a.Employee.Department != null && a.Employee.Department.DepartmentName.ToLower().Contains(normalized)) ||
                    (a.Employee != null && a.Employee.Position != null && a.Employee.Position.PositionName.ToLower().Contains(normalized)) ||
                    a.Status.ToLower().Contains(normalized) ||
                    (a.Note != null && a.Note.ToLower().Contains(normalized)));
            }

            var attendances = await query.OrderByDescending(a => a.WorkDate).ThenBy(a => a.EmployeeId).ToListAsync();
            var rows = new List<string[]> { new[] { "Work Date", "Employee Code", "Full Name", "Department", "Position", "Status", "Check In", "Check Out", "Working Hours", "Source", "Note" } };
            rows.AddRange(attendances.Select(a => new[]
            {
                a.WorkDate.ToString("yyyy-MM-dd"),
                a.Employee?.EmployeeCode ?? string.Empty,
                a.Employee?.FullName ?? string.Empty,
                a.Employee?.Department?.DepartmentName ?? string.Empty,
                a.Employee?.Position?.PositionName ?? string.Empty,
                a.Status,
                a.CheckInTime.ToString("yyyy-MM-dd HH:mm"),
                a.CheckOutTime?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty,
                a.CheckOutTime.HasValue ? Math.Round((decimal)(a.CheckOutTime.Value - a.CheckInTime).TotalHours, 2).ToString("0.##", CultureInfo.InvariantCulture) : string.Empty,
                a.SourceType ?? string.Empty,
                a.Note ?? string.Empty
            }));
            return BuildReportFile(rows, "Attendance Operations Report", "Attendance records filtered by employee, status, and period", $"attendance-report-{DateTime.Now:yyyyMMddHHmmss}", format, "Attendances");
        }

        [HttpGet("payrolls/export")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> ExportPayrolls([FromQuery] int? year, [FromQuery] int? month, [FromQuery] int? employeeId, [FromQuery] string? format = "csv")
        {
            var query = _context.Payrolls.AsNoTracking().Include(p => p.Employee).ThenInclude(e => e.Department).Include(p => p.Employee).ThenInclude(e => e.Position).AsQueryable();
            if (year.HasValue) query = query.Where(p => p.PayrollYear == year.Value);
            if (month.HasValue) query = query.Where(p => p.PayrollMonth == month.Value);
            if (employeeId.HasValue) query = query.Where(p => p.EmployeeId == employeeId.Value);
            var payrolls = await query.OrderByDescending(p => p.PayrollYear).ThenByDescending(p => p.PayrollMonth).ThenBy(p => p.EmployeeId).ToListAsync();
            var rows = new List<string[]> { new[] { "Payroll Month", "Payroll Year", "Employee Code", "Full Name", "Department", "Position", "Base Salary", "Bonus", "Deduction", "Net Salary", "Generated At" } };
            rows.AddRange(payrolls.Select(p => new[] { p.PayrollMonth.ToString(), p.PayrollYear.ToString(), p.Employee?.EmployeeCode ?? string.Empty, p.Employee?.FullName ?? string.Empty, p.Employee?.Department?.DepartmentName ?? string.Empty, p.Employee?.Position?.PositionName ?? string.Empty, p.BaseSalary.ToString("0.##", CultureInfo.InvariantCulture), p.Bonus.ToString("0.##", CultureInfo.InvariantCulture), p.Deduction.ToString("0.##", CultureInfo.InvariantCulture), p.NetSalary.ToString("0.##", CultureInfo.InvariantCulture), p.GeneratedAt.ToString("yyyy-MM-dd HH:mm") }));
            return BuildReportFile(rows, "Payroll Export", "Payroll records filtered by period and employee", $"payroll-export-{DateTime.Now:yyyyMMddHHmmss}", format, "Payrolls");
        }

        [HttpGet("leaves/export")]
        public async Task<IActionResult> ExportLeaves([FromQuery] string? status, [FromQuery] string? leaveType, [FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? format = "csv")
        {
            var employeeIds = await GetScopedEmployeeIdsAsync();
            var query = _context.LeaveRequests.AsNoTracking().Include(l => l.Employee).ThenInclude(e => e.Department).Where(l => employeeIds.Contains(l.EmployeeId)).AsQueryable();
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(l => l.Status == LeaveStatuses.Normalize(status));
            if (!string.IsNullOrWhiteSpace(leaveType)) query = query.Where(l => l.LeaveType == LeaveTypes.Normalize(leaveType));
            if (year.HasValue) query = query.Where(l => l.StartDate.Year == year.Value || l.EndDate.Year == year.Value);
            if (month.HasValue) query = query.Where(l => l.StartDate.Month == month.Value || l.EndDate.Month == month.Value);
            var leaves = await query.OrderByDescending(l => l.CreatedAt).ToListAsync();
            var rows = new List<string[]> { new[] { "Employee Code", "Employee", "Department", "Leave Type", "Start Date", "End Date", "Total Days", "Status", "Reason", "Approved By", "Approved At" } };
            rows.AddRange(leaves.Select(l => new[] { l.Employee?.EmployeeCode ?? string.Empty, l.Employee?.FullName ?? string.Empty, l.Employee?.Department?.DepartmentName ?? string.Empty, l.LeaveType, l.StartDate.ToString("yyyy-MM-dd"), l.EndDate.ToString("yyyy-MM-dd"), l.TotalDays.ToString(CultureInfo.InvariantCulture), l.Status, l.Reason, l.ApprovedByUserId?.ToString() ?? string.Empty, l.ApprovedAt?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty }));
            return BuildReportFile(rows, "Leave Request Export", "Leave workflow records", $"leave-export-{DateTime.Now:yyyyMMddHHmmss}", format, "Leaves");
        }

        [HttpGet("attendance-adjustments/export")]
        public async Task<IActionResult> ExportAttendanceAdjustments([FromQuery] string? status, [FromQuery] int? employeeId, [FromQuery] int? month, [FromQuery] int? year, [FromQuery] string? search, [FromQuery] string? format = "csv")
        {
            var employeeIds = await GetScopedEmployeeIdsAsync();
            var query = _context.AttendanceAdjustmentRequests.AsNoTracking().Include(x => x.Employee).ThenInclude(e => e.Department).Include(x => x.ReviewedByUser).Where(x => employeeIds.Contains(x.EmployeeId)).AsQueryable();
            if (employeeId.HasValue) query = query.Where(x => x.EmployeeId == employeeId.Value);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == AttendanceAdjustmentStatuses.Normalize(status));
            if (month.HasValue) query = query.Where(x => x.WorkDate.Month == month.Value);
            if (year.HasValue) query = query.Where(x => x.WorkDate.Year == year.Value);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalized = search.Trim().ToLower();
                query = query.Where(x => (x.Employee != null && x.Employee.FullName.ToLower().Contains(normalized)) || (x.Employee != null && x.Employee.EmployeeCode.ToLower().Contains(normalized)) || x.RequestedStatus.ToLower().Contains(normalized) || x.Status.ToLower().Contains(normalized) || x.Reason.ToLower().Contains(normalized) || (x.ReviewNote != null && x.ReviewNote.ToLower().Contains(normalized)));
            }
            var requests = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.WorkDate).ToListAsync();
            var rows = new List<string[]> { new[] { "Created At", "Employee Code", "Full Name", "Department", "Work Date", "Requested Status", "Requested Check In", "Requested Check Out", "Reason", "Request Status", "Reviewed By", "Reviewed At", "Review Note" } };
            rows.AddRange(requests.Select(x => new[] { x.CreatedAt.ToString("yyyy-MM-dd HH:mm"), x.Employee?.EmployeeCode ?? string.Empty, x.Employee?.FullName ?? string.Empty, x.Employee?.Department?.DepartmentName ?? string.Empty, x.WorkDate.ToString("yyyy-MM-dd"), x.RequestedStatus, x.RequestedCheckInTime?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty, x.RequestedCheckOutTime?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty, x.Reason, x.Status, x.ReviewedByUser?.Username ?? string.Empty, x.ReviewedAt?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty, x.ReviewNote ?? string.Empty }));
            return BuildReportFile(rows, "Attendance Adjustment Audit", "Attendance correction workflow and review trail", $"attendance-adjustments-{DateTime.Now:yyyyMMddHHmmss}", format, "Adjustments");
        }

        [HttpGet("leave-audit/export")]
        public async Task<IActionResult> ExportLeaveAudit([FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? format = "csv")
        {
            var employeeIds = await GetScopedEmployeeIdsAsync();
            var query = _context.LeaveRequestAuditLogs.AsNoTracking().Include(x => x.PerformedByUser).Include(x => x.LeaveRequest).ThenInclude(l => l.Employee).Where(x => x.LeaveRequest != null && employeeIds.Contains(x.LeaveRequest.EmployeeId)).AsQueryable();
            if (year.HasValue) query = query.Where(x => x.CreatedAt.Year == year.Value);
            if (month.HasValue) query = query.Where(x => x.CreatedAt.Month == month.Value);
            var logs = await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
            var rows = new List<string[]> { new[] { "Created At", "Action", "Previous Status", "New Status", "Employee Code", "Employee", "Performed By", "Note" } };
            rows.AddRange(logs.Select(x => new[] { x.CreatedAt.ToString("yyyy-MM-dd HH:mm"), x.ActionType, x.PreviousStatus ?? string.Empty, x.NewStatus ?? string.Empty, x.LeaveRequest?.Employee?.EmployeeCode ?? string.Empty, x.LeaveRequest?.Employee?.FullName ?? string.Empty, x.PerformedByUser?.Username ?? string.Empty, x.Note ?? string.Empty }));
            return BuildReportFile(rows, "Leave Audit History", "Audit trail of leave workflow decisions", $"leave-audit-{DateTime.Now:yyyyMMddHHmmss}", format, "Leave Audit");
        }

        [HttpGet("payroll-audit/export")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> ExportPayrollAudit([FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? format = "csv")
        {
            var query = _context.PayrollAuditLogs.AsNoTracking().Include(x => x.PerformedByUser).AsQueryable();
            if (year.HasValue) query = query.Where(x => x.PayrollYear == year.Value);
            if (month.HasValue) query = query.Where(x => x.PayrollMonth == month.Value);
            var logs = await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
            var rows = new List<string[]> { new[] { "Created At", "Action", "Payroll Month", "Payroll Year", "Employee Code", "Employee", "Base Salary", "Bonus", "Deduction", "Net Salary", "Performed By", "Note" } };
            rows.AddRange(logs.Select(x => new[] { x.CreatedAt.ToString("yyyy-MM-dd HH:mm"), x.ActionType, x.PayrollMonth.ToString(), x.PayrollYear.ToString(), x.EmployeeCode, x.EmployeeFullName, x.BaseSalary.ToString("0.##", CultureInfo.InvariantCulture), x.Bonus.ToString("0.##", CultureInfo.InvariantCulture), x.Deduction.ToString("0.##", CultureInfo.InvariantCulture), x.NetSalary.ToString("0.##", CultureInfo.InvariantCulture), x.PerformedByUser?.Username ?? string.Empty, x.Note ?? string.Empty }));
            return BuildReportFile(rows, "Payroll Audit History", "Audit trail of payroll generation and adjustment", $"payroll-audit-{DateTime.Now:yyyyMMddHHmmss}", format, "Payroll Audit");
        }

        [HttpGet("attendance-adjustment-audit/export")]
        public async Task<IActionResult> ExportAttendanceAdjustmentAudit([FromQuery] int? year, [FromQuery] int? month, [FromQuery] int? employeeId, [FromQuery] string? format = "csv")
        {
            var employeeIds = await GetScopedEmployeeIdsAsync();
            var query = _context.AttendanceAdjustmentAuditLogs.AsNoTracking().Include(x => x.PerformedByUser).Where(x => employeeIds.Contains(x.EmployeeId)).AsQueryable();
            if (employeeId.HasValue) query = query.Where(x => x.EmployeeId == employeeId.Value);
            if (year.HasValue) query = query.Where(x => x.WorkDate.Year == year.Value);
            if (month.HasValue) query = query.Where(x => x.WorkDate.Month == month.Value);
            var logs = await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
            var rows = new List<string[]> { new[] { "Created At", "Action", "Employee Code", "Employee", "Work Date", "Requested Status", "Current Status", "Previous Status", "New Status", "Performed By", "Note" } };
            rows.AddRange(logs.Select(x => new[] { x.CreatedAt.ToString("yyyy-MM-dd HH:mm"), x.ActionType, x.EmployeeCode, x.EmployeeFullName, x.WorkDate.ToString("yyyy-MM-dd"), x.RequestedStatus, x.CurrentStatus, x.PreviousStatus ?? string.Empty, x.NewStatus ?? string.Empty, x.PerformedByUser?.Username ?? string.Empty, x.Note ?? string.Empty }));
            return BuildReportFile(rows, "Attendance Adjustment Audit History", "Detailed audit trail of attendance adjustment workflow", $"attendance-adjustment-audit-{DateTime.Now:yyyyMMddHHmmss}", format, "Adjustment Audit");
        }

        [HttpGet("department-comparison")]
        public async Task<IActionResult> GetDepartmentComparison([FromQuery] int year, [FromQuery] int month)
        {
            if (month is < 1 or > 12) return BadRequest("Month must be between 1 and 12.");
            if (year is < 2000 or > 2100) return BadRequest("Year is invalid.");

            var managerDepartmentId = await GetManagedDepartmentIdAsync();
            var cacheKey = $"reports:department-comparison:{User.Identity?.Name}:{year}:{month}:{managerDepartmentId}";
            var cached = await _cache.GetAsync<List<object>>(cacheKey);
            if (cached != null) return Ok(cached);

            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var departmentsQuery = _context.Departments.AsNoTracking().OrderBy(x => x.DepartmentName).AsQueryable();
            if (managerDepartmentId.HasValue) departmentsQuery = departmentsQuery.Where(x => x.Id == managerDepartmentId.Value);
            var departments = await departmentsQuery.ToListAsync();
            var employeesQuery = _context.Employees.AsNoTracking().AsQueryable();
            if (managerDepartmentId.HasValue) employeesQuery = employeesQuery.Where(x => x.DepartmentId == managerDepartmentId.Value);
            var employees = await employeesQuery.ToListAsync();
            var attendances = await _context.Attendances.AsNoTracking().Where(x => x.WorkDate.Year == year && x.WorkDate.Month == month).ToListAsync();
            var payrolls = await _context.Payrolls.AsNoTracking().Where(x => x.PayrollYear == year && x.PayrollMonth == month).ToListAsync();
            var leaves = await _context.LeaveRequests.AsNoTracking().Where(x => x.EndDate >= monthStart && x.StartDate <= monthEnd).ToListAsync();
            var adjustments = await _context.AttendanceAdjustmentRequests.AsNoTracking().Where(x => x.WorkDate.Year == year && x.WorkDate.Month == month).ToListAsync();

            var result = departments.Select(dept =>
            {
                var deptEmployees = employees.Where(x => x.DepartmentId == dept.Id).ToList();
                var ids = deptEmployees.Select(x => x.Id).ToHashSet();
                var deptAttendances = attendances.Where(x => ids.Contains(x.EmployeeId)).ToList();
                var deptPayrolls = payrolls.Where(x => ids.Contains(x.EmployeeId)).ToList();
                var deptLeaves = leaves.Where(x => ids.Contains(x.EmployeeId)).ToList();
                var deptAdjustments = adjustments.Where(x => ids.Contains(x.EmployeeId)).ToList();
                var headcount = deptEmployees.Count;
                var activeCount = deptEmployees.Count(x => x.IsActive);
                return new
                {
                    departmentId = dept.Id,
                    departmentCode = dept.DepartmentCode,
                    departmentName = dept.DepartmentName,
                    headcount,
                    activeEmployees = activeCount,
                    inactiveEmployees = headcount - activeCount,
                    attendanceRecords = deptAttendances.Count,
                    presentCount = deptAttendances.Count(x => x.Status == AttendanceStatuses.Present),
                    lateCount = deptAttendances.Count(x => x.Status == AttendanceStatuses.Late),
                    absentCount = deptAttendances.Count(x => x.Status == AttendanceStatuses.Absent),
                    approvedLeaveCount = deptLeaves.Count(x => x.Status == LeaveStatuses.Approved),
                    pendingLeaveCount = deptLeaves.Count(x => x.Status == LeaveStatuses.Pending),
                    pendingAdjustmentCount = deptAdjustments.Count(x => x.Status == AttendanceAdjustmentStatuses.Pending),
                    payrollCoverage = deptPayrolls.Count,
                    totalNetSalary = deptPayrolls.Sum(x => x.NetSalary),
                    averageNetSalary = deptPayrolls.Any() ? deptPayrolls.Average(x => x.NetSalary) : 0m
                };
            }).Cast<object>().ToList();

            await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));
            return Ok(result);
        }

        [HttpGet("monthly-trends")]
        public async Task<IActionResult> GetMonthlyTrends([FromQuery] int year, [FromQuery] int month, [FromQuery] int monthsBack = 6)
        {
            if (month is < 1 or > 12) return BadRequest("Month must be between 1 and 12.");
            if (year is < 2000 or > 2100) return BadRequest("Year is invalid.");
            if (monthsBack is < 3 or > 18) return BadRequest("MonthsBack must be between 3 and 18.");

            var managerDepartmentId = await GetManagedDepartmentIdAsync();
            var cacheKey = $"reports:monthly-trends:{User.Identity?.Name}:{year}:{month}:{monthsBack}:{managerDepartmentId}";
            var cached = await _cache.GetAsync<List<object>>(cacheKey);
            if (cached != null) return Ok(cached);

            var startAnchor = new DateTime(year, month, 1).AddMonths(-(monthsBack - 1));
            var endAnchor = new DateTime(year, month, 1);
            var endInclusive = endAnchor.AddMonths(1).AddDays(-1);
            var scopedEmployeeIds = managerDepartmentId.HasValue
                ? await _context.Employees.AsNoTracking().Where(e => e.DepartmentId == managerDepartmentId.Value).Select(e => e.Id).ToListAsync()
                : null;

            var attendancesQuery = _context.Attendances.AsNoTracking().Where(x => x.WorkDate >= startAnchor && x.WorkDate <= endInclusive);
            var payrollsQuery = _context.Payrolls.AsNoTracking().Where(x => (x.PayrollYear > startAnchor.Year || (x.PayrollYear == startAnchor.Year && x.PayrollMonth >= startAnchor.Month)) && (x.PayrollYear < endAnchor.Year || (x.PayrollYear == endAnchor.Year && x.PayrollMonth <= endAnchor.Month)));
            var leavesQuery = _context.LeaveRequests.AsNoTracking().Where(x => x.EndDate >= startAnchor && x.StartDate <= endInclusive);
            var adjustmentsQuery = _context.AttendanceAdjustmentRequests.AsNoTracking().Where(x => x.WorkDate >= startAnchor && x.WorkDate <= endInclusive);
            if (scopedEmployeeIds != null)
            {
                attendancesQuery = attendancesQuery.Where(x => scopedEmployeeIds.Contains(x.EmployeeId));
                payrollsQuery = payrollsQuery.Where(x => scopedEmployeeIds.Contains(x.EmployeeId));
                leavesQuery = leavesQuery.Where(x => scopedEmployeeIds.Contains(x.EmployeeId));
                adjustmentsQuery = adjustmentsQuery.Where(x => scopedEmployeeIds.Contains(x.EmployeeId));
            }

            var attendances = await attendancesQuery.ToListAsync();
            var payrolls = await payrollsQuery.ToListAsync();
            var leaves = await leavesQuery.ToListAsync();
            var adjustments = await adjustmentsQuery.ToListAsync();
            var rows = new List<object>();
            for (var cursor = startAnchor; cursor <= endAnchor; cursor = cursor.AddMonths(1))
            {
                var y = cursor.Year;
                var m = cursor.Month;
                var monthStart = new DateTime(y, m, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                var monthAttendances = attendances.Where(x => x.WorkDate.Year == y && x.WorkDate.Month == m).ToList();
                var monthPayrolls = payrolls.Where(x => x.PayrollYear == y && x.PayrollMonth == m).ToList();
                var monthLeaves = leaves.Where(x => x.EndDate >= monthStart && x.StartDate <= monthEnd).ToList();
                var monthAdjustments = adjustments.Where(x => x.WorkDate.Year == y && x.WorkDate.Month == m).ToList();
                rows.Add(new
                {
                    year = y,
                    month = m,
                    periodLabel = $"{m:00}/{y}",
                    attendanceRecords = monthAttendances.Count,
                    presentCount = monthAttendances.Count(x => x.Status == AttendanceStatuses.Present),
                    lateCount = monthAttendances.Count(x => x.Status == AttendanceStatuses.Late),
                    absentCount = monthAttendances.Count(x => x.Status == AttendanceStatuses.Absent),
                    approvedLeaves = monthLeaves.Count(x => x.Status == LeaveStatuses.Approved),
                    pendingLeaves = monthLeaves.Count(x => x.Status == LeaveStatuses.Pending),
                    payrollRecords = monthPayrolls.Count,
                    totalNetSalary = monthPayrolls.Sum(x => x.NetSalary),
                    averageNetSalary = monthPayrolls.Any() ? monthPayrolls.Average(x => x.NetSalary) : 0m,
                    pendingAdjustments = monthAdjustments.Count(x => x.Status == AttendanceAdjustmentStatuses.Pending)
                });
            }

            await _cache.SetAsync(cacheKey, rows, TimeSpan.FromMinutes(5));
            return Ok(rows);
        }

        private async Task<int?> GetManagedDepartmentIdAsync()
        {
            if (!User.IsInRole(UserRoles.Manager)) return null;
            var employeeIdClaim = User.FindFirst("employeeId")?.Value;
            if (!int.TryParse(employeeIdClaim, out var employeeId)) return null;
            return await _context.Employees.AsNoTracking().Where(e => e.Id == employeeId).Select(e => (int?)e.DepartmentId).FirstOrDefaultAsync();
        }

        private async Task<List<int>> GetScopedEmployeeIdsAsync()
        {
            var managerDepartmentId = await GetManagedDepartmentIdAsync();
            var employeesQuery = _context.Employees.AsNoTracking().AsQueryable();
            if (managerDepartmentId.HasValue)
                employeesQuery = employeesQuery.Where(x => x.DepartmentId == managerDepartmentId.Value);
            return await employeesQuery.Select(x => x.Id).ToListAsync();
        }

        private IActionResult BuildReportFile(IReadOnlyList<string[]> rows, string title, string subtitle, string baseFileName, string? format, string sheetName)
        {
            var normalizedFormat = NormalizeFormat(format);
            if (normalizedFormat == "xlsx") return BuildExcelFile(rows, title, baseFileName, sheetName);
            if (normalizedFormat == "pdf") return BuildPdfFile(rows, title, subtitle, baseFileName);
            return BuildCsvFile(rows, $"{baseFileName}.csv");
        }

        private FileContentResult BuildCsvFile(IEnumerable<string[]> rows, string fileName)
        {
            var sb = new StringBuilder();
            foreach (var row in rows) sb.AppendLine(string.Join(",", row.Select(EscapeCsv)));
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
        }

        private FileContentResult BuildExcelFile(IReadOnlyList<string[]> rows, string title, string baseFileName, string sheetName)
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add(sheetName);
            for (var r = 0; r < rows.Count; r++) for (var c = 0; c < rows[r].Length; c++) sheet.Cell(r + 1, c + 1).Value = rows[r][c];
            var headerRange = sheet.Range(1, 1, 1, rows[0].Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCEBFF");
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Columns().AdjustToContents();
            sheet.SheetView.FreezeRows(1);
            sheet.Cell(1, rows[0].Length + 2).Value = title;
            sheet.Cell(2, rows[0].Length + 2).Value = $"Generated {DateTime.Now:yyyy-MM-dd HH:mm}";
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{baseFileName}.xlsx");
        }

        private FileContentResult BuildPdfFile(IReadOnlyList<string[]> rows, string title, string subtitle, string baseFileName)
        {
            var dataRows = rows.Skip(1).ToList();
            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(24);
                    page.DefaultTextStyle(x => x.FontSize(10));
                    page.Header().Column(col =>
                    {
                        col.Item().Text(title).Bold().FontSize(18).FontColor(Colors.Blue.Medium);
                        col.Item().Text(subtitle).FontSize(11);
                        col.Item().Text($"Generated at {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                    page.Content().PaddingVertical(12).Column(col =>
                    {
                        col.Spacing(6);
                        col.Item().Background(Colors.Grey.Lighten4).Padding(8).Text(string.Join("   |   ", rows[0])).Bold();
                        foreach (var row in dataRows)
                            col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).Text(string.Join("   |   ", row));
                    });
                    page.Footer().AlignCenter().Text(text => { text.Span("Page "); text.CurrentPageNumber(); });
                });
            }).GeneratePdf();
            return File(pdf, "application/pdf", $"{baseFileName}.pdf");
        }

        private static string NormalizeFormat(string? format)
        {
            var normalized = (format ?? "csv").Trim().ToLowerInvariant();
            return normalized is "csv" or "xlsx" or "pdf" ? normalized : "csv";
        }

        private static string EscapeCsv(string? value)
        {
            var safe = (value ?? string.Empty).Replace("\"", "\"\"");
            return $"\"{safe}\"";
        }
    }
}
