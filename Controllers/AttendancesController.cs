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
    [Authorize]
    [ApiController]
    public class AttendancesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AttendancesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetAll([FromQuery] AttendanceQueryRequest query)
        {
            var attendanceQuery = BuildAttendanceQuery();

            if (query.EmployeeId.HasValue)
            {
                attendanceQuery = attendanceQuery.Where(a => a.EmployeeId == query.EmployeeId.Value);
            }

            if (query.WorkDate.HasValue)
            {
                var workDate = query.WorkDate.Value.Date;
                attendanceQuery = attendanceQuery.Where(a => a.WorkDate == workDate);
            }

            if (query.Month.HasValue && query.Year.HasValue)
            {
                attendanceQuery = attendanceQuery.Where(a =>
                    a.WorkDate.Month == query.Month.Value &&
                    a.WorkDate.Year == query.Year.Value);
            }
            else if (query.Year.HasValue)
            {
                attendanceQuery = attendanceQuery.Where(a =>
                    a.WorkDate.Year == query.Year.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                var normalizedStatus = AttendanceStatuses.Normalize(query.Status);
                attendanceQuery = attendanceQuery.Where(a => a.Status == normalizedStatus);
            }

            var attendances = await attendanceQuery
                .OrderByDescending(a => a.WorkDate)
                .ThenBy(a => a.EmployeeId)
                .ToListAsync();

            return Ok(attendances.Select(MapToAttendanceResponse));
        }

        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetById(int id)
        {
            var attendance = await BuildAttendanceQuery()
                .FirstOrDefaultAsync(a => a.Id == id);

            if (attendance == null)
                return NotFound();

            return Ok(MapToAttendanceResponse(attendance));
        }

        [HttpGet("employee/{employeeId:int}")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetByEmployee(
            int employeeId,
            [FromQuery] int? month,
            [FromQuery] int? year,
            [FromQuery] DateTime? workDate,
            [FromQuery] string? status)
        {
            var employeeExists = await _context.Employees.AnyAsync(e => e.Id == employeeId);
            if (!employeeExists)
                return NotFound("Employee not found.");

            var attendanceQuery = BuildAttendanceQuery()
                .Where(a => a.EmployeeId == employeeId);

            if (workDate.HasValue)
            {
                var date = workDate.Value.Date;
                attendanceQuery = attendanceQuery.Where(a => a.WorkDate == date);
            }
            else if (month.HasValue && year.HasValue)
            {
                attendanceQuery = attendanceQuery.Where(a =>
                    a.WorkDate.Month == month.Value &&
                    a.WorkDate.Year == year.Value);
            }
            else if (year.HasValue)
            {
                attendanceQuery = attendanceQuery.Where(a =>
                    a.WorkDate.Year == year.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalizedStatus = AttendanceStatuses.Normalize(status);
                attendanceQuery = attendanceQuery.Where(a => a.Status == normalizedStatus);
            }

            var attendances = await attendanceQuery
                .OrderByDescending(a => a.WorkDate)
                .ToListAsync();

            return Ok(attendances.Select(MapToAttendanceResponse));
        }

        [HttpPost]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> Create(CreateAttendanceRequest request)
        {
            var employee = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Position)
                .FirstOrDefaultAsync(e => e.Id == request.EmployeeId);

            if (employee == null)
                return BadRequest("EmployeeId does not exist.");

            var workDate = request.WorkDate.Date;
            var status = AttendanceStatuses.Normalize(request.Status);
            var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

            var attendanceExists = await _context.Attendances
                .AnyAsync(a => a.EmployeeId == request.EmployeeId && a.WorkDate == workDate);

            if (attendanceExists)
                return BadRequest("Attendance for this employee on this work date already exists.");

            var attendance = new Attendance
            {
                EmployeeId = request.EmployeeId,
                WorkDate = workDate,
                CheckInTime = request.CheckInTime,
                CheckOutTime = request.CheckOutTime,
                Status = status,
                Note = note,
                SourceType = AttendanceSourceTypes.Manual
            };

            _context.Attendances.Add(attendance);
            await _context.SaveChangesAsync();

            attendance.Employee = employee;

            return CreatedAtAction(nameof(GetById), new { id = attendance.Id }, MapToAttendanceResponse(attendance));
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> Update(int id, UpdateAttendanceRequest request)
        {
            var attendance = await _context.Attendances.FindAsync(id);
            if (attendance == null)
                return NotFound();

            var employee = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Position)
                .FirstOrDefaultAsync(e => e.Id == request.EmployeeId);

            if (employee == null)
                return BadRequest("EmployeeId does not exist.");

            var workDate = request.WorkDate.Date;
            var status = AttendanceStatuses.Normalize(request.Status);
            var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

            var attendanceExists = await _context.Attendances
                .AnyAsync(a => a.Id != id &&
                               a.EmployeeId == request.EmployeeId &&
                               a.WorkDate == workDate);

            if (attendanceExists)
                return BadRequest("Attendance for this employee on this work date already exists.");

            attendance.EmployeeId = request.EmployeeId;
            attendance.WorkDate = workDate;
            attendance.CheckInTime = request.CheckInTime;
            attendance.CheckOutTime = request.CheckOutTime;
            attendance.Status = status;
            attendance.Note = note;
            attendance.SourceType ??= AttendanceSourceTypes.Manual;

            await _context.SaveChangesAsync();

            attendance.Employee = employee;

            return Ok(MapToAttendanceResponse(attendance));
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var attendance = await _context.Attendances.FindAsync(id);

            if (attendance == null)
                return NotFound();

            var linkedRequests = await _context.AttendanceAdjustmentRequests
                .Where(x => x.AttendanceId == id && x.Status == AttendanceAdjustmentStatuses.Pending)
                .AnyAsync();

            if (linkedRequests)
                return BadRequest("Cannot delete this attendance while a pending adjustment request still references it.");

            _context.Attendances.Remove(attendance);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("adjustment-requests")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetAdjustmentRequests(
            [FromQuery] string? status,
            [FromQuery] int? employeeId,
            [FromQuery] int? departmentId,
            [FromQuery] int? month,
            [FromQuery] int? year,
            [FromQuery] DateTime? workDate,
            [FromQuery] string? search)
        {
            var query = BuildAdjustmentRequestQuery();

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalizedStatus = AttendanceAdjustmentStatuses.Normalize(status);
                query = query.Where(x => x.Status == normalizedStatus);
            }

            if (employeeId.HasValue)
            {
                query = query.Where(x => x.EmployeeId == employeeId.Value);
            }

            if (departmentId.HasValue)
            {
                query = query.Where(x => x.Employee != null && x.Employee.DepartmentId == departmentId.Value);
            }

            if (workDate.HasValue)
            {
                var filterDate = workDate.Value.Date;
                query = query.Where(x => x.WorkDate == filterDate);
            }
            else
            {
                if (month.HasValue)
                    query = query.Where(x => x.WorkDate.Month == month.Value);

                if (year.HasValue)
                    query = query.Where(x => x.WorkDate.Year == year.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = search.Trim().ToLower();
                query = query.Where(x =>
                    (x.Employee != null && x.Employee.FullName.ToLower().Contains(normalizedSearch)) ||
                    (x.Employee != null && x.Employee.EmployeeCode.ToLower().Contains(normalizedSearch)) ||
                    (x.Employee != null && x.Employee.Department != null && x.Employee.Department.DepartmentName.ToLower().Contains(normalizedSearch)) ||
                    x.RequestedStatus.ToLower().Contains(normalizedSearch) ||
                    x.Status.ToLower().Contains(normalizedSearch) ||
                    x.Reason.ToLower().Contains(normalizedSearch) ||
                    (x.ReviewNote != null && x.ReviewNote.ToLower().Contains(normalizedSearch)));
            }

            var requests = await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.WorkDate)
                .ToListAsync();

            return Ok(requests.Select(MapToAdjustmentResponse));
        }

        [HttpGet("adjustment-requests/{id:int}/history")]
        [Authorize]
        public async Task<IActionResult> GetAdjustmentRequestHistory(int id)
        {
            var request = await _context.AttendanceAdjustmentRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (request == null)
                return NotFound();

            if (User.IsInRole(UserRoles.Employee))
            {
                var employeeId = GetCurrentEmployeeId();
                if (!employeeId.HasValue || request.EmployeeId != employeeId.Value)
                    return Forbid();
            }
            else if (!User.IsInRole(UserRoles.Admin) && !User.IsInRole(UserRoles.HR) && !User.IsInRole(UserRoles.Manager))
            {
                return Forbid();
            }

            var history = await _context.AttendanceAdjustmentAuditLogs
                .AsNoTracking()
                .Include(x => x.PerformedByUser)
                .Where(x => x.AttendanceAdjustmentRequestId == id)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return Ok(history.Select(MapAuditResponse));
        }

        [HttpGet("adjustment-history/recent")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetRecentAdjustmentAuditHistory([FromQuery] int take = 12)
        {
            if (take <= 0 || take > 50)
                return BadRequest("Take must be between 1 and 50.");

            var history = await _context.AttendanceAdjustmentAuditLogs
                .AsNoTracking()
                .Include(x => x.PerformedByUser)
                .OrderByDescending(x => x.CreatedAt)
                .Take(take)
                .ToListAsync();

            return Ok(history.Select(MapAuditResponse));
        }

        [HttpGet("my-adjustment-history/recent")]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> GetMyRecentAdjustmentAuditHistory([FromQuery] int take = 12)
        {
            if (take <= 0 || take > 50)
                return BadRequest("Take must be between 1 and 50.");

            var employeeId = GetCurrentEmployeeId();
            if (!employeeId.HasValue)
                return Forbid();

            var history = await _context.AttendanceAdjustmentAuditLogs
                .AsNoTracking()
                .Include(x => x.PerformedByUser)
                .Where(x => x.EmployeeId == employeeId.Value)
                .OrderByDescending(x => x.CreatedAt)
                .Take(take)
                .ToListAsync();

            return Ok(history.Select(MapAuditResponse));
        }

        [HttpGet("my-adjustment-requests")]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> GetMyAdjustmentRequests(
            [FromQuery] string? status,
            [FromQuery] int? month,
            [FromQuery] int? year,
            [FromQuery] DateTime? workDate)
        {
            var employeeId = GetCurrentEmployeeId();
            if (!employeeId.HasValue)
                return Forbid();

            var query = BuildAdjustmentRequestQuery()
                .Where(x => x.EmployeeId == employeeId.Value);

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalizedStatus = AttendanceAdjustmentStatuses.Normalize(status);
                query = query.Where(x => x.Status == normalizedStatus);
            }

            if (workDate.HasValue)
            {
                var filterDate = workDate.Value.Date;
                query = query.Where(x => x.WorkDate == filterDate);
            }
            else
            {
                if (month.HasValue)
                    query = query.Where(x => x.WorkDate.Month == month.Value);

                if (year.HasValue)
                    query = query.Where(x => x.WorkDate.Year == year.Value);
            }

            var requests = await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.WorkDate)
                .ToListAsync();

            return Ok(requests.Select(MapToAdjustmentResponse));
        }

        [HttpPost("adjustment-requests")]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> CreateAdjustmentRequest(CreateAttendanceAdjustmentRequest request)
        {
            var employeeId = GetCurrentEmployeeId();
            if (!employeeId.HasValue)
                return Forbid();

            var employee = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Position)
                .FirstOrDefaultAsync(e => e.Id == employeeId.Value);

            if (employee == null)
                return BadRequest("Employee profile does not exist.");

            var workDate = request.WorkDate.Date;
            var requestedStatus = AttendanceStatuses.Normalize(request.RequestedStatus);
            var trimmedReason = request.Reason.Trim();

            var duplicatePending = await _context.AttendanceAdjustmentRequests
                .AnyAsync(x => x.EmployeeId == employeeId.Value &&
                               x.WorkDate == workDate &&
                               x.Status == AttendanceAdjustmentStatuses.Pending);

            if (duplicatePending)
                return BadRequest("A pending attendance adjustment request already exists for this work date.");

            var existingAttendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == employeeId.Value && a.WorkDate == workDate);

            var normalizedCheckIn = NormalizeRequestedCheckIn(workDate, requestedStatus, request.RequestedCheckInTime);
            var normalizedCheckOut = request.RequestedCheckOutTime;

            if (normalizedCheckOut.HasValue && normalizedCheckIn.HasValue && normalizedCheckOut.Value < normalizedCheckIn.Value)
                return BadRequest("RequestedCheckOutTime cannot be earlier than RequestedCheckInTime.");

            var adjustmentRequest = new AttendanceAdjustmentRequest
            {
                EmployeeId = employeeId.Value,
                AttendanceId = existingAttendance?.Id,
                WorkDate = workDate,
                RequestedCheckInTime = normalizedCheckIn,
                RequestedCheckOutTime = normalizedCheckOut,
                RequestedStatus = requestedStatus,
                Reason = trimmedReason,
                Status = AttendanceAdjustmentStatuses.Pending,
                CreatedAt = DateTime.Now
            };

            _context.AttendanceAdjustmentRequests.Add(adjustmentRequest);
            await _context.SaveChangesAsync();

            adjustmentRequest.Employee = employee;
            adjustmentRequest.Attendance = existingAttendance;
            await AppendAdjustmentAuditLogAsync(
                adjustmentRequest,
                "Submitted",
                null,
                adjustmentRequest.Status,
                "Employee submitted an attendance adjustment request.",
                GetCurrentUserId());
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMyAdjustmentRequests), new { id = adjustmentRequest.Id }, MapToAdjustmentResponse(adjustmentRequest));
        }

        [HttpPut("adjustment-requests/{id:int}/approve")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> ApproveAdjustmentRequest(int id, ReviewAttendanceAdjustmentRequest request)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized();

            var adjustmentRequest = await _context.AttendanceAdjustmentRequests
                .Include(x => x.Employee)
                    .ThenInclude(e => e.Department)
                .Include(x => x.Employee)
                    .ThenInclude(e => e.Position)
                .Include(x => x.ReviewedByUser)
                .Include(x => x.Attendance)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (adjustmentRequest == null)
                return NotFound();

            if (adjustmentRequest.Status != AttendanceAdjustmentStatuses.Pending)
                return BadRequest("Only pending adjustment requests can be approved.");

            var attendance = adjustmentRequest.AttendanceId.HasValue
                ? await _context.Attendances.FirstOrDefaultAsync(a => a.Id == adjustmentRequest.AttendanceId.Value)
                : await _context.Attendances.FirstOrDefaultAsync(a => a.EmployeeId == adjustmentRequest.EmployeeId && a.WorkDate == adjustmentRequest.WorkDate);

            if (attendance == null)
            {
                attendance = new Attendance
                {
                    EmployeeId = adjustmentRequest.EmployeeId,
                    WorkDate = adjustmentRequest.WorkDate
                };
                _context.Attendances.Add(attendance);
            }
            else
            {
                var duplicateByDate = await _context.Attendances.AnyAsync(a =>
                    a.Id != attendance.Id &&
                    a.EmployeeId == adjustmentRequest.EmployeeId &&
                    a.WorkDate == adjustmentRequest.WorkDate);

                if (duplicateByDate)
                    return BadRequest("Another attendance record already exists for this employee and work date.");
            }

            attendance.WorkDate = adjustmentRequest.WorkDate;
            attendance.EmployeeId = adjustmentRequest.EmployeeId;
            attendance.CheckInTime = adjustmentRequest.RequestedCheckInTime ?? adjustmentRequest.WorkDate.Date;
            attendance.CheckOutTime = adjustmentRequest.RequestedCheckOutTime;
            attendance.Status = AttendanceStatuses.Normalize(adjustmentRequest.RequestedStatus);
            attendance.SourceType = AttendanceSourceTypes.Manual;
            attendance.SourceReferenceId = adjustmentRequest.Id;
            attendance.Note = BuildApprovedAttendanceNote(adjustmentRequest, request.ReviewNote);

            adjustmentRequest.Attendance = attendance;
            adjustmentRequest.Status = AttendanceAdjustmentStatuses.Approved;
            adjustmentRequest.ReviewedByUserId = userId.Value;
            adjustmentRequest.ReviewedAt = DateTime.Now;
            adjustmentRequest.ReviewNote = string.IsNullOrWhiteSpace(request.ReviewNote) ? null : request.ReviewNote.Trim();

            await _context.SaveChangesAsync();

            if (adjustmentRequest.AttendanceId != attendance.Id)
            {
                adjustmentRequest.AttendanceId = attendance.Id;
                await _context.SaveChangesAsync();
            }

            await AppendAdjustmentAuditLogAsync(
                adjustmentRequest,
                "Approved",
                AttendanceAdjustmentStatuses.Pending,
                adjustmentRequest.Status,
                adjustmentRequest.ReviewNote ?? "Attendance adjustment was approved.",
                userId.Value);
            await _context.SaveChangesAsync();

            adjustmentRequest.Attendance = attendance;
            adjustmentRequest.ReviewedByUser = await _context.AppUsers.FindAsync(userId.Value);

            return Ok(MapToAdjustmentResponse(adjustmentRequest));
        }

        [HttpPut("adjustment-requests/{id:int}/reject")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> RejectAdjustmentRequest(int id, ReviewAttendanceAdjustmentRequest request)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized();

            var adjustmentRequest = await _context.AttendanceAdjustmentRequests
                .Include(x => x.Employee)
                    .ThenInclude(e => e.Department)
                .Include(x => x.Employee)
                    .ThenInclude(e => e.Position)
                .Include(x => x.ReviewedByUser)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (adjustmentRequest == null)
                return NotFound();

            if (adjustmentRequest.Status != AttendanceAdjustmentStatuses.Pending)
                return BadRequest("Only pending adjustment requests can be rejected.");

            if (string.IsNullOrWhiteSpace(request.ReviewNote))
                return BadRequest("ReviewNote is required when rejecting an attendance adjustment request.");

            adjustmentRequest.Status = AttendanceAdjustmentStatuses.Rejected;
            adjustmentRequest.ReviewedByUserId = userId.Value;
            adjustmentRequest.ReviewedAt = DateTime.Now;
            adjustmentRequest.ReviewNote = request.ReviewNote.Trim();

            await _context.SaveChangesAsync();
            await AppendAdjustmentAuditLogAsync(
                adjustmentRequest,
                "Rejected",
                AttendanceAdjustmentStatuses.Pending,
                adjustmentRequest.Status,
                adjustmentRequest.ReviewNote,
                userId.Value);
            await _context.SaveChangesAsync();

            adjustmentRequest.ReviewedByUser = await _context.AppUsers.FindAsync(userId.Value);

            return Ok(MapToAdjustmentResponse(adjustmentRequest));
        }

        [HttpGet("my-attendances")]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> GetMyAttendances(
            [FromQuery] int? month,
            [FromQuery] int? year,
            [FromQuery] DateTime? workDate,
            [FromQuery] string? status)
        {
            var employeeId = GetCurrentEmployeeId();
            if (!employeeId.HasValue)
                return Forbid();

            var attendanceQuery = BuildAttendanceQuery()
                .Where(a => a.EmployeeId == employeeId.Value);

            if (workDate.HasValue)
            {
                var date = workDate.Value.Date;
                attendanceQuery = attendanceQuery.Where(a => a.WorkDate == date);
            }
            else if (month.HasValue && year.HasValue)
            {
                attendanceQuery = attendanceQuery.Where(a =>
                    a.WorkDate.Month == month.Value &&
                    a.WorkDate.Year == year.Value);
            }
            else if (year.HasValue)
            {
                attendanceQuery = attendanceQuery.Where(a =>
                    a.WorkDate.Year == year.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalizedStatus = AttendanceStatuses.Normalize(status);
                attendanceQuery = attendanceQuery.Where(a => a.Status == normalizedStatus);
            }

            var attendances = await attendanceQuery
                .OrderByDescending(a => a.WorkDate)
                .ToListAsync();

            return Ok(attendances.Select(MapToAttendanceResponse));
        }

        private IQueryable<Attendance> BuildAttendanceQuery()
        {
            return _context.Attendances
                .AsNoTracking()
                .Include(a => a.Employee)
                    .ThenInclude(e => e.Department)
                .Include(a => a.Employee)
                    .ThenInclude(e => e.Position);
        }

        private IQueryable<AttendanceAdjustmentRequest> BuildAdjustmentRequestQuery()
        {
            return _context.AttendanceAdjustmentRequests
                .AsNoTracking()
                .Include(x => x.Employee)
                    .ThenInclude(e => e.Department)
                .Include(x => x.Employee)
                    .ThenInclude(e => e.Position)
                .Include(x => x.ReviewedByUser);
        }

        private static AttendanceResponse MapToAttendanceResponse(Attendance attendance)
        {
            decimal? workingHours = null;

            if (attendance.CheckOutTime.HasValue)
            {
                workingHours = Math.Round(
                    (decimal)(attendance.CheckOutTime.Value - attendance.CheckInTime).TotalHours,
                    2);
            }

            return new AttendanceResponse
            {
                Id = attendance.Id,
                EmployeeId = attendance.EmployeeId,
                EmployeeCode = attendance.Employee?.EmployeeCode ?? string.Empty,
                FullName = attendance.Employee?.FullName ?? string.Empty,
                DepartmentId = attendance.Employee?.DepartmentId ?? 0,
                DepartmentName = attendance.Employee?.Department?.DepartmentName ?? string.Empty,
                PositionId = attendance.Employee?.PositionId ?? 0,
                PositionName = attendance.Employee?.Position?.PositionName ?? string.Empty,
                WorkDate = attendance.WorkDate,
                CheckInTime = attendance.CheckInTime,
                CheckOutTime = attendance.CheckOutTime,
                Status = attendance.Status,
                Note = attendance.Note,
                WorkingHours = workingHours,
                SourceType = attendance.SourceType,
                SourceReferenceId = attendance.SourceReferenceId,
            };
        }

        private static AttendanceAdjustmentRequestResponse MapToAdjustmentResponse(AttendanceAdjustmentRequest request)
        {
            return new AttendanceAdjustmentRequestResponse
            {
                Id = request.Id,
                EmployeeId = request.EmployeeId,
                EmployeeCode = request.Employee?.EmployeeCode ?? string.Empty,
                FullName = request.Employee?.FullName ?? string.Empty,
                DepartmentId = request.Employee?.DepartmentId ?? 0,
                DepartmentName = request.Employee?.Department?.DepartmentName ?? string.Empty,
                PositionId = request.Employee?.PositionId ?? 0,
                PositionName = request.Employee?.Position?.PositionName ?? string.Empty,
                AttendanceId = request.AttendanceId,
                WorkDate = request.WorkDate,
                RequestedCheckInTime = request.RequestedCheckInTime,
                RequestedCheckOutTime = request.RequestedCheckOutTime,
                RequestedStatus = request.RequestedStatus,
                Reason = request.Reason,
                Status = request.Status,
                CreatedAt = request.CreatedAt,
                ReviewedByUserId = request.ReviewedByUserId,
                ReviewedByUsername = request.ReviewedByUser?.Username,
                ReviewedAt = request.ReviewedAt,
                ReviewNote = request.ReviewNote
            };
        }

        private static DateTime? NormalizeRequestedCheckIn(DateTime workDate, string requestedStatus, DateTime? requestedCheckInTime)
        {
            if (requestedCheckInTime.HasValue)
                return requestedCheckInTime.Value;

            if (requestedStatus == AttendanceStatuses.Absent || requestedStatus == AttendanceStatuses.Leave)
                return workDate.Date;

            return null;
        }

        private static string BuildApprovedAttendanceNote(AttendanceAdjustmentRequest request, string? reviewNote)
        {
            var note = $"Approved attendance adjustment request #{request.Id}. Requested status={request.RequestedStatus}. Reason={request.Reason}.";

            if (!string.IsNullOrWhiteSpace(reviewNote))
            {
                note += $" Review note={reviewNote.Trim()}.";
            }

            return note;
        }

        private Task AppendAdjustmentAuditLogAsync(AttendanceAdjustmentRequest request, string actionType, string? previousStatus, string? newStatus, string? note, int? performedByUserId)
        {
            var snapshot = JsonSerializer.Serialize(new
            {
                request.Id,
                request.EmployeeId,
                EmployeeCode = request.Employee?.EmployeeCode,
                EmployeeFullName = request.Employee?.FullName,
                request.WorkDate,
                request.RequestedStatus,
                request.RequestedCheckInTime,
                request.RequestedCheckOutTime,
                request.Reason,
                request.Status,
                request.CreatedAt,
                request.ReviewedAt,
                request.ReviewNote,
                request.AttendanceId
            });

            _context.AttendanceAdjustmentAuditLogs.Add(new AttendanceAdjustmentAuditLog
            {
                AttendanceAdjustmentRequestId = request.Id,
                EmployeeId = request.EmployeeId,
                EmployeeCode = request.Employee?.EmployeeCode ?? string.Empty,
                EmployeeFullName = request.Employee?.FullName ?? string.Empty,
                WorkDate = request.WorkDate,
                RequestedStatus = request.RequestedStatus,
                CurrentStatus = request.Status,
                PerformedByUserId = performedByUserId,
                ActionType = actionType,
                PreviousStatus = previousStatus,
                NewStatus = newStatus,
                Note = note,
                SnapshotJson = snapshot,
                CreatedAt = DateTime.Now
            });

            return Task.CompletedTask;
        }

        private static AttendanceAdjustmentAuditLogResponse MapAuditResponse(AttendanceAdjustmentAuditLog item)
        {
            return new AttendanceAdjustmentAuditLogResponse
            {
                Id = item.Id,
                AttendanceAdjustmentRequestId = item.AttendanceAdjustmentRequestId,
                EmployeeId = item.EmployeeId,
                EmployeeCode = item.EmployeeCode,
                EmployeeFullName = item.EmployeeFullName,
                WorkDate = item.WorkDate,
                RequestedStatus = item.RequestedStatus,
                CurrentStatus = item.CurrentStatus,
                ActionType = item.ActionType,
                PreviousStatus = item.PreviousStatus,
                NewStatus = item.NewStatus,
                Note = item.Note,
                SnapshotJson = item.SnapshotJson,
                PerformedByUserId = item.PerformedByUserId,
                PerformedByUsername = item.PerformedByUser?.Username,
                CreatedAt = item.CreatedAt
            };
        }

        private int? GetCurrentEmployeeId()
        {
            var employeeIdClaim = User.FindFirst("employeeId")?.Value;

            if (string.IsNullOrWhiteSpace(employeeIdClaim))
                return null;

            return int.TryParse(employeeIdClaim, out var employeeId) ? employeeId : null;
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userIdClaim))
                return null;

            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}
