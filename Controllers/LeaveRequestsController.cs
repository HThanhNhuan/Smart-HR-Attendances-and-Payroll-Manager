using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using smart_hr_attendance_payroll_management.Common;
using smart_hr_attendance_payroll_management.Data;
using smart_hr_attendance_payroll_management.DTOs;
using smart_hr_attendance_payroll_management.Entities;
using System.Security.Claims;
using System.Text.Json;
using smart_hr_attendance_payroll_management.Services;

namespace smart_hr_attendance_payroll_management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LeaveRequestsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly DistributedCacheService _cache;
        private readonly LeaveBalancePolicyService _leaveBalancePolicy;
        public LeaveRequestsController(AppDbContext context, DistributedCacheService cache, LeaveBalancePolicyService leaveBalancePolicy) { _context = context; _cache = cache; _leaveBalancePolicy = leaveBalancePolicy; }

        [HttpGet]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetAll([FromQuery] LeaveRequestQueryRequest query)
        {
            var leaveQuery = BuildQuery();
            var managerDepartmentId = await GetManagedDepartmentIdAsync();
            if (managerDepartmentId.HasValue)
                leaveQuery = leaveQuery.Where(l => l.Employee != null && l.Employee.DepartmentId == managerDepartmentId.Value);
            if (query.EmployeeId.HasValue) leaveQuery = leaveQuery.Where(l => l.EmployeeId == query.EmployeeId.Value);
            if (!string.IsNullOrWhiteSpace(query.Status)) leaveQuery = leaveQuery.Where(l => l.Status == LeaveStatuses.Normalize(query.Status));
            if (!string.IsNullOrWhiteSpace(query.LeaveType)) leaveQuery = leaveQuery.Where(l => l.LeaveType == LeaveTypes.Normalize(query.LeaveType));
            if (query.FromDate.HasValue) leaveQuery = leaveQuery.Where(l => l.EndDate >= query.FromDate.Value.Date);
            if (query.ToDate.HasValue) leaveQuery = leaveQuery.Where(l => l.StartDate <= query.ToDate.Value.Date);
            var leaveRequests = await leaveQuery.OrderByDescending(l => l.CreatedAt).ToListAsync();
            return Ok(leaveRequests.Select(MapToResponse));
        }

        [HttpGet("pending")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetPending()
        {
            var leaveQuery = BuildQuery().Where(l => l.Status == LeaveStatuses.Pending);
            var managerDepartmentId = await GetManagedDepartmentIdAsync();
            if (managerDepartmentId.HasValue)
                leaveQuery = leaveQuery.Where(l => l.Employee != null && l.Employee.DepartmentId == managerDepartmentId.Value);
            var leaveRequests = await leaveQuery.OrderByDescending(l => l.CreatedAt).ToListAsync();
            return Ok(leaveRequests.Select(MapToResponse));
        }

        [HttpGet("my-requests")]
        [Authorize(Roles = "Employee,Manager")]
        public async Task<IActionResult> GetMyRequests([FromQuery] LeaveRequestQueryRequest query)
        {
            var employeeId = GetCurrentEmployeeId();
            if (!employeeId.HasValue) return Forbid();
            var leaveQuery = BuildQuery().Where(l => l.EmployeeId == employeeId.Value);
            if (!string.IsNullOrWhiteSpace(query.Status)) leaveQuery = leaveQuery.Where(l => l.Status == LeaveStatuses.Normalize(query.Status));
            if (!string.IsNullOrWhiteSpace(query.LeaveType)) leaveQuery = leaveQuery.Where(l => l.LeaveType == LeaveTypes.Normalize(query.LeaveType));
            if (query.FromDate.HasValue) leaveQuery = leaveQuery.Where(l => l.EndDate >= query.FromDate.Value.Date);
            if (query.ToDate.HasValue) leaveQuery = leaveQuery.Where(l => l.StartDate <= query.ToDate.Value.Date);
            var leaveRequests = await leaveQuery.OrderByDescending(l => l.CreatedAt).ToListAsync();
            return Ok(leaveRequests.Select(MapToResponse));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var leaveRequest = await BuildQuery().FirstOrDefaultAsync(l => l.Id == id);
            if (leaveRequest == null) return NotFound();
            var isAdminOrHr = User.IsInRole(UserRoles.Admin) || User.IsInRole(UserRoles.HR) || User.IsInRole(UserRoles.Manager);
            if (!isAdminOrHr)
            {
                var employeeId = GetCurrentEmployeeId();
                if (!employeeId.HasValue || leaveRequest.EmployeeId != employeeId.Value) return NotFound();
            }
            else if (User.IsInRole(UserRoles.Manager))
            {
                var managerDepartmentId = await GetManagedDepartmentIdAsync();
                if (managerDepartmentId.HasValue && leaveRequest.Employee?.DepartmentId != managerDepartmentId.Value) return NotFound();
            }
            return Ok(MapToResponse(leaveRequest));
        }

        [HttpGet("{id:int}/history")]
        public async Task<IActionResult> GetHistory(int id)
        {
            var leaveRequest = await _context.LeaveRequests.AsNoTracking().Include(l => l.Employee).FirstOrDefaultAsync(l => l.Id == id);
            if (leaveRequest == null) return NotFound();
            var isAdminOrHr = User.IsInRole(UserRoles.Admin) || User.IsInRole(UserRoles.HR) || User.IsInRole(UserRoles.Manager);
            if (!isAdminOrHr)
            {
                var employeeId = GetCurrentEmployeeId();
                if (!employeeId.HasValue || leaveRequest.EmployeeId != employeeId.Value) return NotFound();
            }
            else if (User.IsInRole(UserRoles.Manager))
            {
                var managerDepartmentId = await GetManagedDepartmentIdAsync();
                if (managerDepartmentId.HasValue && leaveRequest.Employee?.DepartmentId != managerDepartmentId.Value) return NotFound();
            }
            var history = await _context.LeaveRequestAuditLogs.AsNoTracking().Include(x => x.PerformedByUser).Where(x => x.LeaveRequestId == id).OrderByDescending(x => x.CreatedAt).ToListAsync();
            return Ok(history.Select(MapAuditResponse));
        }

        [HttpGet("history/recent")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetRecentHistory([FromQuery] int take = 8)
        {
            if (take <= 0 || take > 30) return BadRequest("Take must be between 1 and 30.");
            IQueryable<LeaveRequestAuditLog> historyQuery = _context.LeaveRequestAuditLogs.AsNoTracking().Include(x => x.PerformedByUser);
            var managerDepartmentId = await GetManagedDepartmentIdAsync();
            if (managerDepartmentId.HasValue)
            {
                var employeeIds = await _context.Employees.AsNoTracking().Where(e => e.DepartmentId == managerDepartmentId.Value).Select(e => e.Id).ToListAsync();
                historyQuery = historyQuery.Where(x => x.LeaveRequest != null && employeeIds.Contains(x.LeaveRequest.EmployeeId));
            }
            var history = await historyQuery.OrderByDescending(x => x.CreatedAt).Take(take).ToListAsync();
            return Ok(history.Select(MapAuditResponse));
        }

        [HttpGet("my-balance")]
        [Authorize(Roles = "Employee,Manager")]
        public async Task<IActionResult> GetMyBalance([FromQuery] int? year)
        {
            var employeeId = GetCurrentEmployeeId();
            if (!employeeId.HasValue) return Forbid();
            var targetYear = year ?? DateTime.Now.Year;
            var cacheKey = BuildLeaveBalanceCacheKey(employeeId.Value, targetYear);
            var response = await _cache.GetOrCreateAsync(cacheKey, TimeSpan.FromMinutes(5), async () =>
            {
                var balance = await GetOrCreateLeaveBalanceAsync(employeeId.Value, targetYear);
                return MapBalance(balance);
            });
            return Ok(response);
        }

        [HttpGet("balances")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetBalances([FromQuery] int? year)
        {
            var targetYear = year ?? DateTime.Now.Year;
            var balanceQuery = _context.LeaveBalances
                .AsNoTracking()
                .Include(x => x.Employee).ThenInclude(e => e.Department)
                .Where(x => x.Year == targetYear);
            var managerDepartmentId = await GetManagedDepartmentIdAsync();
            if (managerDepartmentId.HasValue)
                balanceQuery = balanceQuery.Where(x => x.Employee != null && x.Employee.DepartmentId == managerDepartmentId.Value);
            var cacheKey = $"leave-balances:list:{targetYear}:{managerDepartmentId}:{User.Identity?.Name}";
            var response = await _cache.GetOrCreateAsync(cacheKey, TimeSpan.FromMinutes(5), async () =>
            {
                var balances = await balanceQuery
                    .OrderBy(x => x.Employee!.FullName)
                    .ToListAsync();
                return balances.Select(MapBalance).ToList();
            });
            return Ok(response);
        }

        [HttpPut("balances/{employeeId:int}")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> UpdateBalance(int employeeId, [FromBody] UpdateLeaveBalanceRequest request, [FromQuery] int? year)
        {
            var employee = await _context.Employees.Include(e => e.Department).FirstOrDefaultAsync(e => e.Id == employeeId);
            if (employee == null) return NotFound();
            var targetYear = year ?? DateTime.Now.Year;
            var balance = await GetOrCreateLeaveBalanceAsync(employeeId, targetYear);
            balance.AnnualAllocated = request.AnnualAllocated;
            balance.AnnualUsed = request.AnnualUsed;
            balance.SickAllocated = request.SickAllocated;
            balance.SickUsed = request.SickUsed;
            balance.CarryForward = request.CarryForward;
            balance.UnpaidDays = request.UnpaidDays;
            balance.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            await InvalidateLeaveBalanceCacheAsync(employeeId, targetYear);
            return Ok(MapBalance(balance));
        }

        [HttpPost]
        [Authorize(Roles = "Employee,Manager")]
        public async Task<IActionResult> Create(CreateLeaveRequestRequest request)
        {
            var employeeId = GetCurrentEmployeeId();
            if (!employeeId.HasValue) return Forbid();
            var employee = await _context.Employees.Include(e => e.Department).Include(e => e.Position).FirstOrDefaultAsync(e => e.Id == employeeId.Value);
            if (employee == null) return BadRequest("Employee profile does not exist.");
            if (!LeaveTypes.IsValid(request.LeaveType)) return BadRequest($"LeaveType must be one of: {string.Join(", ", LeaveTypes.All)}.");
            var leaveType = LeaveTypes.Normalize(request.LeaveType);
            var startDate = request.StartDate.Date;
            var endDate = request.EndDate.Date;
            var reason = request.Reason.Trim();
            if (endDate < startDate) return BadRequest("EndDate cannot be earlier than StartDate.");
            var overlapExists = await _context.LeaveRequests.AnyAsync(l => l.EmployeeId == employeeId.Value && (l.Status == LeaveStatuses.Pending || l.Status == LeaveStatuses.Approved) && l.StartDate <= endDate && l.EndDate >= startDate);
            if (overlapExists) return BadRequest("This leave request overlaps with an existing pending or approved leave request.");
            var totalDays = await CalculateLeaveChargeDaysAsync(employeeId.Value, startDate, endDate);
            if (totalDays <= 0) return BadRequest("Leave request does not cover any scheduled/working day.");
            var availabilityError = await ValidateLeaveAvailabilityAsync(employeeId.Value, leaveType, totalDays, startDate.Year, null);
            if (availabilityError != null) return BadRequest(availabilityError);
            var leaveRequest = new LeaveRequest { EmployeeId = employeeId.Value, LeaveType = leaveType, StartDate = startDate, EndDate = endDate, TotalDays = totalDays, Reason = reason, Status = LeaveStatuses.Pending, CreatedAt = DateTime.Now };
            _context.LeaveRequests.Add(leaveRequest);
            await _context.SaveChangesAsync();
            await InvalidateLeaveBalanceCacheAsync(employeeId.Value, startDate.Year);
            leaveRequest.Employee = employee;
            await AppendLeaveAuditLogAsync(leaveRequest, "Submitted", null, leaveRequest.Status, "Employee submitted a leave request.", null);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = leaveRequest.Id }, MapToResponse(leaveRequest));
        }

        [HttpPut("{id:int}/cancel")]
        [Authorize(Roles = "Employee,Manager")]
        public async Task<IActionResult> Cancel(int id)
        {
            var employeeId = GetCurrentEmployeeId();
            if (!employeeId.HasValue) return Forbid();
            var leaveRequest = await _context.LeaveRequests.Include(l => l.Employee).ThenInclude(e => e.Department).Include(l => l.Employee).ThenInclude(e => e.Position).Include(l => l.ApprovedByUser).FirstOrDefaultAsync(l => l.Id == id && l.EmployeeId == employeeId.Value);
            if (leaveRequest == null) return NotFound();
            if (leaveRequest.Status != LeaveStatuses.Pending) return BadRequest("Only pending leave requests can be cancelled.");
            var managerAccessError = await ValidateManagerDepartmentAccessAsync(leaveRequest.EmployeeId);
            if (managerAccessError != null) return managerAccessError;
            var previousStatus = leaveRequest.Status;
            leaveRequest.Status = LeaveStatuses.Cancelled;
            await _context.SaveChangesAsync();
            await InvalidateLeaveBalanceCacheAsync(leaveRequest.EmployeeId, leaveRequest.StartDate.Year);
            await AppendLeaveAuditLogAsync(leaveRequest, "Cancelled", previousStatus, leaveRequest.Status, "Employee cancelled a pending leave request.", null);
            await _context.SaveChangesAsync();
            return Ok(MapToResponse(leaveRequest));
        }

        [HttpPut("{id:int}/approve")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> Approve(int id, ApproveLeaveRequest request)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();
            var leaveRequest = await _context.LeaveRequests.Include(l => l.Employee).ThenInclude(e => e.Department).Include(l => l.Employee).ThenInclude(e => e.Position).Include(l => l.ApprovedByUser).FirstOrDefaultAsync(l => l.Id == id);
            if (leaveRequest == null) return NotFound();
            if (leaveRequest.Status != LeaveStatuses.Pending) return BadRequest("Only pending leave requests can be approved.");
            var startDate = leaveRequest.StartDate.Date;
            var endDate = leaveRequest.EndDate.Date;
            var existingAttendances = await _context.Attendances.Where(a => a.EmployeeId == leaveRequest.EmployeeId && a.WorkDate >= startDate && a.WorkDate <= endDate).ToListAsync();
            if (existingAttendances.Any())
            {
                var conflictDates = existingAttendances.Select(a => a.WorkDate.ToString("yyyy-MM-dd")).ToList();
                return BadRequest(new { message = "Cannot approve leave because attendance records already exist in the requested date range.", conflictDates });
            }
            var managerAccessError = await ValidateManagerDepartmentAccessAsync(leaveRequest.EmployeeId);
            if (managerAccessError != null) return managerAccessError;
            var previousStatus = leaveRequest.Status;
            var availabilityError = await ValidateLeaveAvailabilityAsync(leaveRequest.EmployeeId, leaveRequest.LeaveType, leaveRequest.TotalDays, leaveRequest.StartDate.Year, leaveRequest.Id);
            if (availabilityError != null) return BadRequest(availabilityError);
            var balance = await GetOrCreateLeaveBalanceAsync(leaveRequest.EmployeeId, leaveRequest.StartDate.Year);
            if (leaveRequest.LeaveType == LeaveTypes.AnnualLeave)
            {
                balance.AnnualUsed += leaveRequest.TotalDays;
            }
            else if (leaveRequest.LeaveType == LeaveTypes.SickLeave)
            {
                balance.SickUsed += leaveRequest.TotalDays;
            }
            else if (leaveRequest.LeaveType == LeaveTypes.UnpaidLeave || leaveRequest.LeaveType == LeaveTypes.Other)
            {
                balance.UnpaidDays += leaveRequest.TotalDays;
            }
            balance.UpdatedAt = DateTime.Now;
            leaveRequest.Status = LeaveStatuses.Approved;
            leaveRequest.ApprovedByUserId = userId.Value;
            leaveRequest.ApprovedAt = DateTime.Now;
            leaveRequest.ApprovalNote = string.IsNullOrWhiteSpace(request.ApprovalNote) ? null : request.ApprovalNote.Trim();
            leaveRequest.RejectionReason = null;
            var attendances = new List<Attendance>();
            var workingDates = await GetLeaveWorkingDatesAsync(leaveRequest.EmployeeId, startDate, endDate);
            foreach (var date in workingDates)
            {
                attendances.Add(new Attendance { EmployeeId = leaveRequest.EmployeeId, WorkDate = date, CheckInTime = date, CheckOutTime = null, Status = AttendanceStatuses.Leave, SourceType = AttendanceSourceTypes.ApprovedLeave, SourceReferenceId = leaveRequest.Id, Note = $"Auto-generated from approved leave request #{leaveRequest.Id}. Type={leaveRequest.LeaveType}; Range={leaveRequest.StartDate:yyyy-MM-dd} to {leaveRequest.EndDate:yyyy-MM-dd}; ApprovedByUserId={userId.Value}." });
            }
            _context.Attendances.AddRange(attendances);
            await _context.SaveChangesAsync();
            await InvalidateLeaveBalanceCacheAsync(leaveRequest.EmployeeId, leaveRequest.StartDate.Year);
            leaveRequest.ApprovedByUser = await _context.AppUsers.FindAsync(userId.Value);
            await AppendLeaveAuditLogAsync(leaveRequest, "Approved", previousStatus, leaveRequest.Status, leaveRequest.ApprovalNote ?? "Leave request approved and attendance records were generated.", userId.Value);
            await _context.SaveChangesAsync();
            return Ok(MapToResponse(leaveRequest));
        }

        [HttpPut("{id:int}/reject")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> Reject(int id, RejectLeaveRequest request)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();
            var leaveRequest = await _context.LeaveRequests.Include(l => l.Employee).ThenInclude(e => e.Department).Include(l => l.Employee).ThenInclude(e => e.Position).Include(l => l.ApprovedByUser).FirstOrDefaultAsync(l => l.Id == id);
            if (leaveRequest == null) return NotFound();
            if (leaveRequest.Status != LeaveStatuses.Pending) return BadRequest("Only pending leave requests can be rejected.");
            var managerAccessError = await ValidateManagerDepartmentAccessAsync(leaveRequest.EmployeeId);
            if (managerAccessError != null) return managerAccessError;
            var previousStatus = leaveRequest.Status;
            leaveRequest.Status = LeaveStatuses.Rejected;
            leaveRequest.ApprovedByUserId = userId.Value;
            leaveRequest.ApprovedAt = DateTime.Now;
            leaveRequest.ApprovalNote = null;
            leaveRequest.RejectionReason = request.RejectionReason.Trim();
            await _context.SaveChangesAsync();
            await InvalidateLeaveBalanceCacheAsync(leaveRequest.EmployeeId, leaveRequest.StartDate.Year);
            leaveRequest.ApprovedByUser = await _context.AppUsers.FindAsync(userId.Value);
            await AppendLeaveAuditLogAsync(leaveRequest, "Rejected", previousStatus, leaveRequest.Status, leaveRequest.RejectionReason, userId.Value);
            await _context.SaveChangesAsync();
            return Ok(MapToResponse(leaveRequest));
        }

        [HttpGet("dashboard/monthly")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetMonthlyDashboard([FromQuery] int year, [FromQuery] int month)
        {
            if (month < 1 || month > 12) return BadRequest("Month must be between 1 and 12.");
            if (year < 2000 || year > 2100) return BadRequest("Year is invalid.");
            var leaveQuery = _context.LeaveRequests.AsNoTracking().Where(l => l.StartDate.Year == year && l.StartDate.Month == month || l.EndDate.Year == year && l.EndDate.Month == month || (l.StartDate.Year < year || (l.StartDate.Year == year && l.StartDate.Month < month)) && (l.EndDate.Year > year || (l.EndDate.Year == year && l.EndDate.Month > month)));
            var managerDepartmentId = await GetManagedDepartmentIdAsync();
            if (managerDepartmentId.HasValue)
            {
                var employeeIds = await _context.Employees.AsNoTracking().Where(e => e.DepartmentId == managerDepartmentId.Value).Select(e => e.Id).ToListAsync();
                leaveQuery = leaveQuery.Where(l => employeeIds.Contains(l.EmployeeId));
            }
            var leaveRequests = await leaveQuery.ToListAsync();
            var response = new LeaveMonthlyDashboardResponse
            {
                Year = year,
                Month = month,
                TotalRequests = leaveRequests.Count,
                PendingRequests = leaveRequests.Count(l => l.Status == LeaveStatuses.Pending),
                ApprovedRequests = leaveRequests.Count(l => l.Status == LeaveStatuses.Approved),
                RejectedRequests = leaveRequests.Count(l => l.Status == LeaveStatuses.Rejected),
                CancelledRequests = leaveRequests.Count(l => l.Status == LeaveStatuses.Cancelled),
                ApprovedLeaveDays = leaveRequests.Where(l => l.Status == LeaveStatuses.Approved).Sum(l => CalculateDaysWithinMonth(l.StartDate, l.EndDate, year, month)),
                AnnualLeaveRequests = leaveRequests.Count(l => l.LeaveType == LeaveTypes.AnnualLeave),
                SickLeaveRequests = leaveRequests.Count(l => l.LeaveType == LeaveTypes.SickLeave),
                UnpaidLeaveRequests = leaveRequests.Count(l => l.LeaveType == LeaveTypes.UnpaidLeave),
                OtherLeaveRequests = leaveRequests.Count(l => l.LeaveType == LeaveTypes.Other)
            };
            return Ok(response);
        }

        private IQueryable<LeaveRequest> BuildQuery() => _context.LeaveRequests.AsNoTracking().Include(l => l.Employee).ThenInclude(e => e.Department).Include(l => l.Employee).ThenInclude(e => e.Position).Include(l => l.ApprovedByUser);

        private Task AppendLeaveAuditLogAsync(LeaveRequest leaveRequest, string actionType, string? previousStatus, string? newStatus, string? note, int? performedByUserId)
        {
            var snapshot = JsonSerializer.Serialize(new { leaveRequest.Id, leaveRequest.EmployeeId, leaveRequest.LeaveType, StartDate = leaveRequest.StartDate.ToString("yyyy-MM-dd"), EndDate = leaveRequest.EndDate.ToString("yyyy-MM-dd"), leaveRequest.TotalDays, leaveRequest.Status, leaveRequest.Reason, leaveRequest.ApprovedByUserId, leaveRequest.ApprovedAt, leaveRequest.ApprovalNote, leaveRequest.RejectionReason, leaveRequest.CreatedAt });
            _context.LeaveRequestAuditLogs.Add(new LeaveRequestAuditLog { LeaveRequestId = leaveRequest.Id, PerformedByUserId = performedByUserId, ActionType = actionType, PreviousStatus = previousStatus, NewStatus = newStatus, Note = note, SnapshotJson = snapshot, CreatedAt = DateTime.Now });
            return Task.CompletedTask;
        }

        private static LeaveRequestResponse MapToResponse(LeaveRequest leaveRequest) => new LeaveRequestResponse
        {
            Id = leaveRequest.Id,
            EmployeeId = leaveRequest.EmployeeId,
            EmployeeCode = leaveRequest.Employee?.EmployeeCode ?? string.Empty,
            FullName = leaveRequest.Employee?.FullName ?? string.Empty,
            DepartmentId = leaveRequest.Employee?.DepartmentId ?? 0,
            DepartmentName = leaveRequest.Employee?.Department?.DepartmentName ?? string.Empty,
            PositionId = leaveRequest.Employee?.PositionId ?? 0,
            PositionName = leaveRequest.Employee?.Position?.PositionName ?? string.Empty,
            LeaveType = leaveRequest.LeaveType,
            StartDate = leaveRequest.StartDate,
            EndDate = leaveRequest.EndDate,
            TotalDays = leaveRequest.TotalDays,
            Reason = leaveRequest.Reason,
            Status = leaveRequest.Status,
            ApprovedByUserId = leaveRequest.ApprovedByUserId,
            ApprovedByUsername = leaveRequest.ApprovedByUser?.Username,
            ApprovedAt = leaveRequest.ApprovedAt,
            ApprovalNote = leaveRequest.ApprovalNote,
            RejectionReason = leaveRequest.RejectionReason,
            CreatedAt = leaveRequest.CreatedAt
        };

        private static LeaveRequestAuditLogResponse MapAuditResponse(LeaveRequestAuditLog item) => new LeaveRequestAuditLogResponse
        {
            Id = item.Id,
            LeaveRequestId = item.LeaveRequestId,
            ActionType = item.ActionType,
            PreviousStatus = item.PreviousStatus,
            NewStatus = item.NewStatus,
            Note = item.Note,
            SnapshotJson = item.SnapshotJson,
            PerformedByUserId = item.PerformedByUserId,
            PerformedByUsername = item.PerformedByUser?.Username,
            CreatedAt = item.CreatedAt
        };

        private async Task<LeaveBalance> GetOrCreateLeaveBalanceAsync(int employeeId, int year)
        {
            var balance = await _context.LeaveBalances
                .Include(x => x.Employee).ThenInclude(e => e.Department)
                .FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.Year == year);
            if (balance == null)
            {
                var employee = await _context.Employees.Include(e => e.Department).FirstAsync(e => e.Id == employeeId);
                var previousYearBalance = await _context.LeaveBalances.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.Year == year - 1);
                var carryForward = previousYearBalance == null ? 0 : _leaveBalancePolicy.CalculateCarryForward(previousYearBalance.AnnualAllocated, previousYearBalance.CarryForward, previousYearBalance.AnnualUsed);
                var annualAllocated = CalculateAnnualAllocation(employee.HireDate, year);
                balance = new LeaveBalance
                {
                    EmployeeId = employeeId,
                    Year = year,
                    AnnualAllocated = annualAllocated,
                    SickAllocated = 6,
                    CarryForward = carryForward,
                    UpdatedAt = DateTime.Now
                };
                _context.LeaveBalances.Add(balance);
                await _context.SaveChangesAsync();
                balance = await _context.LeaveBalances.Include(x => x.Employee).ThenInclude(e => e.Department).FirstAsync(x => x.Id == balance.Id);
            }
            else
            {
                var employee = balance.Employee ?? await _context.Employees.Include(e => e.Department).FirstAsync(e => e.Id == employeeId);
                var annualAllocated = CalculateAnnualAllocation(employee.HireDate, year);
                if (balance.AnnualAllocated != annualAllocated)
                {
                    balance.AnnualAllocated = annualAllocated;
                    balance.UpdatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
            }
            return balance;
        }

        private LeaveBalanceResponse MapBalance(LeaveBalance balance)
        {
            var pendingAnnual = _context.LeaveRequests.AsNoTracking().Where(x => x.EmployeeId == balance.EmployeeId && x.Status == LeaveStatuses.Pending && x.StartDate.Year == balance.Year && x.LeaveType == LeaveTypes.AnnualLeave).Sum(x => (decimal?)x.TotalDays) ?? 0m;
            var pendingSick = _context.LeaveRequests.AsNoTracking().Where(x => x.EmployeeId == balance.EmployeeId && x.Status == LeaveStatuses.Pending && x.StartDate.Year == balance.Year && x.LeaveType == LeaveTypes.SickLeave).Sum(x => (decimal?)x.TotalDays) ?? 0m;
            var annualRemaining = (balance.AnnualAllocated + balance.CarryForward) - balance.AnnualUsed;
            var sickRemaining = balance.SickAllocated - balance.SickUsed;
            return new LeaveBalanceResponse
            {
                EmployeeId = balance.EmployeeId,
                EmployeeCode = balance.Employee?.EmployeeCode ?? string.Empty,
                FullName = balance.Employee?.FullName ?? string.Empty,
                DepartmentId = balance.Employee?.DepartmentId ?? 0,
                DepartmentName = balance.Employee?.Department?.DepartmentName ?? string.Empty,
                Year = balance.Year,
                AnnualAllocated = balance.AnnualAllocated,
                AnnualUsed = balance.AnnualUsed,
                AnnualRemaining = annualRemaining,
                SickAllocated = balance.SickAllocated,
                SickUsed = balance.SickUsed,
                SickRemaining = sickRemaining,
                CarryForward = balance.CarryForward,
                AnnualPending = pendingAnnual,
                SickPending = pendingSick,
                AnnualAvailableAfterPending = annualRemaining - pendingAnnual,
                SickAvailableAfterPending = sickRemaining - pendingSick,
                UnpaidDays = balance.UnpaidDays,
                UpdatedAt = balance.UpdatedAt
            };
        }

        private async Task<string?> ValidateLeaveAvailabilityAsync(int employeeId, string leaveType, int totalDays, int year, int? excludeRequestId)
        {
            var balance = await GetOrCreateLeaveBalanceAsync(employeeId, year);
            var pendingByType = await _context.LeaveRequests.AsNoTracking()
                .Where(x => x.EmployeeId == employeeId && x.Status == LeaveStatuses.Pending && x.StartDate.Year == year && (!excludeRequestId.HasValue || x.Id != excludeRequestId.Value) && x.LeaveType == leaveType)
                .SumAsync(x => (decimal?)x.TotalDays) ?? 0m;
            if (leaveType == LeaveTypes.AnnualLeave)
            {
                var available = _leaveBalancePolicy.GetAnnualAvailableAfterPending(balance.AnnualAllocated, balance.CarryForward, balance.AnnualUsed, pendingByType);
                if (available < totalDays) return $"Annual leave balance is insufficient after reserving pending requests. Available: {available:0.##} day(s).";
            }
            else if (leaveType == LeaveTypes.SickLeave)
            {
                var available = _leaveBalancePolicy.GetSickAvailableAfterPending(balance.SickAllocated, balance.SickUsed, pendingByType);
                if (available < totalDays) return $"Sick leave balance is insufficient after reserving pending requests. Available: {available:0.##} day(s).";
            }
            return null;
        }

        private async Task<int> CalculateLeaveChargeDaysAsync(int employeeId, DateTime startDate, DateTime endDate)
        {
            var scheduledDates = await GetLeaveWorkingDatesAsync(employeeId, startDate, endDate);
            return scheduledDates.Count;
        }

        private async Task<List<DateTime>> GetLeaveWorkingDatesAsync(int employeeId, DateTime startDate, DateTime endDate)
        {
            var scheduledDates = await _context.WorkSchedules.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.WorkDate >= startDate.Date && x.WorkDate <= endDate.Date).Select(x => x.WorkDate.Date).OrderBy(x => x).ToListAsync();
            if (scheduledDates.Count > 0) return scheduledDates.Distinct().ToList();
            var days = new List<DateTime>();
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                days.Add(date);
            }
            return days;
        }


        private static string BuildLeaveBalanceCacheKey(int employeeId, int year) => $"leave-balance:{employeeId}:{year}";

        private async Task InvalidateLeaveBalanceCacheAsync(int employeeId, int year)
        {
            await _cache.RemoveAsync(BuildLeaveBalanceCacheKey(employeeId, year));
            await _cache.RemoveAsync($"leave-balances:list:{year}::");
        }

        private decimal CalculateAnnualAllocation(DateTime hireDate, int year)
        {
            if (hireDate.Year < year) return 12m;
            if (hireDate.Year > year) return 0m;
            var months = 12 - hireDate.Month + 1;
            return Math.Max(Math.Round(months * 1m, 2), 0m);
        }

        private async Task<IActionResult?> ValidateManagerDepartmentAccessAsync(int employeeId)
        {
            var managerDepartmentId = await GetManagedDepartmentIdAsync();
            if (!managerDepartmentId.HasValue) return null;
            var employeeDepartmentId = await _context.Employees.AsNoTracking().Where(e => e.Id == employeeId).Select(e => (int?)e.DepartmentId).FirstOrDefaultAsync();
            if (!employeeDepartmentId.HasValue || employeeDepartmentId.Value != managerDepartmentId.Value) return Forbid();
            return null;
        }

        private async Task<int?> GetManagedDepartmentIdAsync()
        {
            if (!User.IsInRole(UserRoles.Manager)) return null;
            var employeeId = GetCurrentEmployeeId();
            if (!employeeId.HasValue) return null;
            return await _context.Employees.AsNoTracking().Where(e => e.Id == employeeId.Value).Select(e => (int?)e.DepartmentId).FirstOrDefaultAsync();
        }

        private int? GetCurrentEmployeeId() => int.TryParse(User.FindFirst("employeeId")?.Value, out var employeeId) ? employeeId : null;
        private int? GetCurrentUserId() => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : null;
        private static int CalculateTotalDays(DateTime startDate, DateTime endDate)
        {
            var total = 0;
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                total++;
            }
            return total;
        }
        private static int CalculateDaysWithinMonth(DateTime startDate, DateTime endDate, int year, int month)
        {
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var effectiveStart = startDate.Date > monthStart ? startDate.Date : monthStart;
            var effectiveEnd = endDate.Date < monthEnd ? endDate.Date : monthEnd;
            return effectiveEnd < effectiveStart ? 0 : (effectiveEnd - effectiveStart).Days + 1;
        }
    }
}
