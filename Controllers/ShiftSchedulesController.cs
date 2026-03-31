using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using smart_hr_attendance_payroll_management.Data;
using smart_hr_attendance_payroll_management.DTOs;
using smart_hr_attendance_payroll_management.Entities;

namespace smart_hr_attendance_payroll_management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,HR,Manager")]
    public class ShiftSchedulesController : ControllerBase
    {
        private readonly AppDbContext _context;
        public ShiftSchedulesController(AppDbContext context) { _context = context; }

        [HttpGet("shifts")]
        public async Task<IActionResult> GetShifts() => Ok((await _context.WorkShifts.AsNoTracking().OrderBy(x => x.ShiftName).ToListAsync()).Select(MapShift));

        [HttpPost("shifts")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> CreateShift(CreateWorkShiftRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ShiftCode) || string.IsNullOrWhiteSpace(request.ShiftName)) return BadRequest("Shift code and shift name are required.");
            if (await _context.WorkShifts.AnyAsync(x => x.ShiftCode == request.ShiftCode.Trim())) return BadRequest("Shift code already exists.");
            var entity = new WorkShift { ShiftCode = request.ShiftCode.Trim(), ShiftName = request.ShiftName.Trim(), StartTime = request.StartTime, EndTime = request.EndTime, StandardHours = request.StandardHours <= 0 ? 8 : request.StandardHours, IsNightShift = request.IsNightShift, Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(), IsActive = true, CreatedAt = DateTime.Now };
            _context.WorkShifts.Add(entity);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetShifts), new { id = entity.Id }, MapShift(entity));
        }

        [HttpPut("shifts/{id:int}")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> UpdateShift(int id, UpdateWorkShiftRequest request)
        {
            var entity = await _context.WorkShifts.FindAsync(id);
            if (entity == null) return NotFound();
            if (await _context.WorkShifts.AnyAsync(x => x.Id != id && x.ShiftCode == request.ShiftCode.Trim())) return BadRequest("Shift code already exists.");
            entity.ShiftCode = request.ShiftCode.Trim();
            entity.ShiftName = request.ShiftName.Trim();
            entity.StartTime = request.StartTime;
            entity.EndTime = request.EndTime;
            entity.StandardHours = request.StandardHours <= 0 ? entity.StandardHours : request.StandardHours;
            entity.IsNightShift = request.IsNightShift;
            entity.IsActive = request.IsActive;
            entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            await _context.SaveChangesAsync();
            return Ok(MapShift(entity));
        }

        [HttpDelete("shifts/{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteShift(int id)
        {
            var entity = await _context.WorkShifts.FindAsync(id);
            if (entity == null) return NotFound();
            var linked = await _context.WorkSchedules.AnyAsync(x => x.WorkShiftId == id);
            if (linked) return BadRequest("Cannot delete a shift that is already assigned in schedules.");
            _context.WorkShifts.Remove(entity);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("schedules")]
        public async Task<IActionResult> GetSchedules([FromQuery] int? employeeId, [FromQuery] int? departmentId, [FromQuery] int? month, [FromQuery] int? year)
        {
            var query = _context.WorkSchedules.AsNoTracking().Include(x => x.Employee).ThenInclude(e => e.Department).Include(x => x.WorkShift).AsQueryable();
            if (employeeId.HasValue) query = query.Where(x => x.EmployeeId == employeeId.Value);
            if (departmentId.HasValue) query = query.Where(x => x.Employee != null && x.Employee.DepartmentId == departmentId.Value);
            if (month.HasValue) query = query.Where(x => x.WorkDate.Month == month.Value);
            if (year.HasValue) query = query.Where(x => x.WorkDate.Year == year.Value);
            var rows = await query.OrderByDescending(x => x.WorkDate).ThenBy(x => x.EmployeeId).ToListAsync();
            return Ok(rows.Select(MapSchedule));
        }

        [HttpPost("schedules")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> CreateSchedule(CreateWorkScheduleRequest request)
        {
            var employee = await _context.Employees.Include(e => e.Department).FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.IsActive);
            if (employee == null) return BadRequest("EmployeeId does not exist.");
            var shift = await _context.WorkShifts.FirstOrDefaultAsync(x => x.Id == request.WorkShiftId && x.IsActive);
            if (shift == null) return BadRequest("WorkShiftId does not exist or is inactive.");
            var workDate = request.WorkDate.Date;
            if (await _context.WorkSchedules.AnyAsync(x => x.EmployeeId == request.EmployeeId && x.WorkDate == workDate)) return BadRequest("Schedule for this employee and work date already exists.");
            var entity = new WorkSchedule { EmployeeId = request.EmployeeId, WorkShiftId = request.WorkShiftId, WorkDate = workDate, Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(), CreatedAt = DateTime.Now };
            _context.WorkSchedules.Add(entity);
            await _context.SaveChangesAsync();
            entity.Employee = employee; entity.WorkShift = shift;
            return CreatedAtAction(nameof(GetSchedules), new { id = entity.Id }, MapSchedule(entity));
        }

        [HttpPut("schedules/{id:int}")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> UpdateSchedule(int id, UpdateWorkScheduleRequest request)
        {
            var entity = await _context.WorkSchedules.Include(x => x.Employee).ThenInclude(e => e.Department).Include(x => x.WorkShift).FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();
            if (entity.IsLocked && !User.IsInRole("Admin")) return BadRequest("Locked schedules can only be changed by Admin.");
            if (await _context.WorkSchedules.AnyAsync(x => x.Id != id && x.EmployeeId == request.EmployeeId && x.WorkDate == request.WorkDate.Date)) return BadRequest("Schedule for this employee and work date already exists.");
            var employee = await _context.Employees.Include(e => e.Department).FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.IsActive);
            var shift = await _context.WorkShifts.FirstOrDefaultAsync(x => x.Id == request.WorkShiftId && x.IsActive);
            if (employee == null || shift == null) return BadRequest("Employee or shift is invalid.");
            entity.EmployeeId = request.EmployeeId;
            entity.WorkShiftId = request.WorkShiftId;
            entity.WorkDate = request.WorkDate.Date;
            entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            entity.IsLocked = request.IsLocked;
            await _context.SaveChangesAsync();
            entity.Employee = employee; entity.WorkShift = shift;
            return Ok(MapSchedule(entity));
        }

        [HttpDelete("schedules/{id:int}")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> DeleteSchedule(int id)
        {
            var entity = await _context.WorkSchedules.FindAsync(id);
            if (entity == null) return NotFound();
            if (entity.IsLocked && !User.IsInRole("Admin")) return BadRequest("Locked schedules can only be removed by Admin.");
            _context.WorkSchedules.Remove(entity);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static WorkShiftResponse MapShift(WorkShift x) => new() { Id = x.Id, ShiftCode = x.ShiftCode, ShiftName = x.ShiftName, StartTime = x.StartTime, EndTime = x.EndTime, StandardHours = x.StandardHours, IsNightShift = x.IsNightShift, IsActive = x.IsActive, Notes = x.Notes, CreatedAt = x.CreatedAt };
        private static WorkScheduleResponse MapSchedule(WorkSchedule x) => new() { Id = x.Id, EmployeeId = x.EmployeeId, EmployeeCode = x.Employee?.EmployeeCode ?? string.Empty, FullName = x.Employee?.FullName ?? string.Empty, DepartmentId = x.Employee?.DepartmentId ?? 0, DepartmentName = x.Employee?.Department?.DepartmentName ?? string.Empty, WorkShiftId = x.WorkShiftId, ShiftCode = x.WorkShift?.ShiftCode ?? string.Empty, ShiftName = x.WorkShift?.ShiftName ?? string.Empty, WorkDate = x.WorkDate, Notes = x.Notes, IsLocked = x.IsLocked, CreatedAt = x.CreatedAt };
    }
}
