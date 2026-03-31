using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using smart_hr_attendance_payroll_management.Common;
using smart_hr_attendance_payroll_management.Data;
using smart_hr_attendance_payroll_management.DTOs;
using smart_hr_attendance_payroll_management.Entities;
using System.Security.Claims;

namespace smart_hr_attendance_payroll_management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OvertimeRequestsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public OvertimeRequestsController(AppDbContext context) { _context = context; }

        [HttpGet]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] int? employeeId, [FromQuery] int? month, [FromQuery] int? year)
        {
            var query = BuildQuery();
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == OvertimeStatuses.Normalize(status));
            if (employeeId.HasValue) query = query.Where(x => x.EmployeeId == employeeId.Value);
            if (month.HasValue) query = query.Where(x => x.WorkDate.Month == month.Value);
            if (year.HasValue) query = query.Where(x => x.WorkDate.Year == year.Value);
            var rows = await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
            return Ok(rows.Select(Map));
        }

        [HttpGet("my-requests")]
        [Authorize(Roles = "Employee,Manager")]
        public async Task<IActionResult> GetMyRequests([FromQuery] string? status, [FromQuery] int? month, [FromQuery] int? year)
        {
            var employeeId = GetCurrentEmployeeId();
            if (!employeeId.HasValue) return Forbid();
            var query = BuildQuery().Where(x => x.EmployeeId == employeeId.Value);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == OvertimeStatuses.Normalize(status));
            if (month.HasValue) query = query.Where(x => x.WorkDate.Month == month.Value);
            if (year.HasValue) query = query.Where(x => x.WorkDate.Year == year.Value);
            var rows = await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
            return Ok(rows.Select(Map));
        }

        [HttpGet("summary")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetSummary([FromQuery] int? month, [FromQuery] int? year)
        {
            month ??= DateTime.Now.Month; year ??= DateTime.Now.Year;
            var rows = await _context.OvertimeRequests.AsNoTracking().Where(x => x.WorkDate.Month == month && x.WorkDate.Year == year).ToListAsync();
            return Ok(new
            {
                month,
                year,
                totalRequests = rows.Count,
                pendingRequests = rows.Count(x => x.Status == OvertimeStatuses.Pending),
                approvedRequests = rows.Count(x => x.Status == OvertimeStatuses.Approved),
                rejectedRequests = rows.Count(x => x.Status == OvertimeStatuses.Rejected),
                approvedHours = rows.Where(x => x.Status == OvertimeStatuses.Approved).Sum(x => x.Hours),
                appliedToPayroll = rows.Count(x => x.AppliedToPayroll)
            });
        }

        [HttpPost]
        [Authorize(Roles = "Employee,Manager")]
        public async Task<IActionResult> Create(CreateOvertimeRequest request)
        {
            var employeeId = GetCurrentEmployeeId();
            if (!employeeId.HasValue) return Forbid();
            var employee = await _context.Employees.Include(e => e.Department).FirstOrDefaultAsync(e => e.Id == employeeId.Value);
            if (employee == null) return BadRequest("Employee profile does not exist.");
            if (request.EndTime <= request.StartTime) return BadRequest("End time must be later than start time.");
            var hours = Math.Round((decimal)(request.EndTime - request.StartTime).TotalHours, 2);
            if (hours <= 0) return BadRequest("Hours must be greater than zero.");
            var entity = new OvertimeRequest { EmployeeId = employeeId.Value, WorkDate = request.WorkDate.Date, StartTime = request.StartTime, EndTime = request.EndTime, Hours = hours, Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Overtime request" : request.Reason.Trim(), Status = OvertimeStatuses.Pending, CreatedAt = DateTime.Now };
            _context.OvertimeRequests.Add(entity);
            await _context.SaveChangesAsync();
            entity.Employee = employee;
            return CreatedAtAction(nameof(GetMyRequests), new { id = entity.Id }, Map(entity));
        }

        [HttpPut("{id:int}/cancel")]
        [Authorize(Roles = "Employee,Manager")]
        public async Task<IActionResult> Cancel(int id)
        {
            var employeeId = GetCurrentEmployeeId();
            if (!employeeId.HasValue) return Forbid();
            var entity = await _context.OvertimeRequests.Include(x => x.Employee).ThenInclude(e => e.Department).Include(x => x.ApprovedByUser).FirstOrDefaultAsync(x => x.Id == id && x.EmployeeId == employeeId.Value);
            if (entity == null) return NotFound();
            if (entity.Status != OvertimeStatuses.Pending) return BadRequest("Only pending overtime requests can be cancelled.");
            entity.Status = OvertimeStatuses.Cancelled;
            await _context.SaveChangesAsync();
            return Ok(Map(entity));
        }

        [HttpPut("{id:int}/approve")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> Approve(int id, ReviewOvertimeRequest request)
        {
            var userId = GetCurrentUserId();
            var entity = await _context.OvertimeRequests.Include(x => x.Employee).ThenInclude(e => e.Department).Include(x => x.ApprovedByUser).FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();
            if (entity.Status != OvertimeStatuses.Pending) return BadRequest("Only pending requests can be approved.");
            entity.Status = OvertimeStatuses.Approved;
            entity.ApprovedByUserId = userId;
            entity.ApprovedAt = DateTime.Now;
            entity.ApprovalNote = string.IsNullOrWhiteSpace(request.ApprovalNote) ? null : request.ApprovalNote.Trim();
            entity.RejectionReason = null;
            await _context.SaveChangesAsync();
            entity.ApprovedByUser = await _context.AppUsers.FindAsync(userId);
            return Ok(Map(entity));
        }

        [HttpPut("{id:int}/reject")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> Reject(int id, ReviewOvertimeRequest request)
        {
            var userId = GetCurrentUserId();
            var entity = await _context.OvertimeRequests.Include(x => x.Employee).ThenInclude(e => e.Department).Include(x => x.ApprovedByUser).FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();
            if (entity.Status != OvertimeStatuses.Pending) return BadRequest("Only pending requests can be rejected.");
            entity.Status = OvertimeStatuses.Rejected;
            entity.ApprovedByUserId = userId;
            entity.ApprovedAt = DateTime.Now;
            entity.ApprovalNote = null;
            entity.RejectionReason = string.IsNullOrWhiteSpace(request.RejectionReason) ? "Rejected" : request.RejectionReason.Trim();
            await _context.SaveChangesAsync();
            entity.ApprovedByUser = await _context.AppUsers.FindAsync(userId);
            return Ok(Map(entity));
        }

        private IQueryable<OvertimeRequest> BuildQuery() => _context.OvertimeRequests.AsNoTracking().Include(x => x.Employee).ThenInclude(e => e.Department).Include(x => x.ApprovedByUser);
        private static OvertimeRequestResponse Map(OvertimeRequest x) => new() { Id = x.Id, EmployeeId = x.EmployeeId, EmployeeCode = x.Employee?.EmployeeCode ?? string.Empty, FullName = x.Employee?.FullName ?? string.Empty, DepartmentId = x.Employee?.DepartmentId ?? 0, DepartmentName = x.Employee?.Department?.DepartmentName ?? string.Empty, WorkDate = x.WorkDate, StartTime = x.StartTime, EndTime = x.EndTime, Hours = x.Hours, Reason = x.Reason, Status = x.Status, ApprovedByUserId = x.ApprovedByUserId, ApprovedByUsername = x.ApprovedByUser?.Username, ApprovedAt = x.ApprovedAt, ApprovalNote = x.ApprovalNote, RejectionReason = x.RejectionReason, AppliedToPayroll = x.AppliedToPayroll, CreatedAt = x.CreatedAt };
        private int? GetCurrentEmployeeId() => int.TryParse(User.FindFirst("employeeId")?.Value, out var employeeId) ? employeeId : null;
        private int? GetCurrentUserId() => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : null;
    }
}
