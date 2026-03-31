using Microsoft.EntityFrameworkCore;
using smart_hr_attendance_payroll_management.Common;
using smart_hr_attendance_payroll_management.Entities;
using smart_hr_attendance_payroll_management.Services;
using System.Text.Json;

namespace smart_hr_attendance_payroll_management.Data
{
    public static class DbSeeder
    {
        private const int TargetAdminUsers = 20;
        private const int TargetHrUsers = 20;
        private const int TargetManagerUsers = 20;
        private const int TargetEmployeeUsers = 25;
        private const int TargetTotalEmployees = 75;

        private static readonly string[] DepartmentSeeds =
        {
            "Human Resources",
            "Accounting",
            "Information Technology",
            "Operations",
            "Sales",
            "Marketing",
            "Customer Success",
            "Logistics",
            "Procurement",
            "Compliance"
        };

        private static readonly string[] PositionSeeds =
        {
            "HR Executive",
            "Accountant",
            "Software Engineer",
            "Operations Specialist",
            "Sales Executive",
            "Marketing Specialist",
            "Support Executive",
            "Logistics Coordinator",
            "Procurement Officer",
            "Team Lead",
            "Supervisor",
            "Analyst"
        };

        private static readonly string[] FirstNames =
        {
            "An", "Binh", "Chi", "Dung", "Giang", "Hanh", "Hieu", "Hoa", "Hung", "Khanh",
            "Lan", "Linh", "Minh", "My", "Nam", "Nga", "Ngoc", "Nhi", "Phong", "Phuc",
            "Quang", "Quynh", "Son", "Tam", "Thao", "Thien", "Thu", "Trang", "Trung", "Tuan",
            "Van", "Vy", "Yen", "Bao", "Diem", "Kiet", "Ngan", "Phuong", "Trinh", "Uyen"
        };

        private static readonly string[] LastNames =
        {
            "Nguyen", "Tran", "Le", "Pham", "Hoang", "Vo", "Phan", "Dang", "Bui", "Do"
        };

        public static async Task SeedAdminAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var passwordService = scope.ServiceProvider.GetRequiredService<PasswordService>();
            var random = new Random(20260327);
            var now = DateTime.Now;

            await EnsureDepartmentsAsync(context);
            await EnsurePositionsAsync(context);
            await context.SaveChangesAsync();

            await EnsureEmployeesAsync(context, TargetTotalEmployees, random, now);
            await context.SaveChangesAsync();

            await EnsureStandaloneRoleUsersAsync(context, passwordService, UserRoles.Admin, "admin", "admin123", TargetAdminUsers);
            await context.SaveChangesAsync();

            await EnsureLinkedRoleUsersAsync(context, passwordService, UserRoles.HR, "hr", "hr123", TargetHrUsers);
            await context.SaveChangesAsync();

            await EnsureLinkedRoleUsersAsync(context, passwordService, UserRoles.Manager, "manager", "manager123", TargetManagerUsers);
            await context.SaveChangesAsync();

            await EnsureLinkedRoleUsersAsync(context, passwordService, UserRoles.Employee, "emp", "emp123", TargetEmployeeUsers);
            await context.SaveChangesAsync();

            var employees = await context.Employees
                .OrderBy(e => e.Id)
                .ToListAsync();

            var reviewers = await context.AppUsers
                .Where(u => u.IsActive && (u.Role == UserRoles.Admin || u.Role == UserRoles.HR || u.Role == UserRoles.Manager))
                .OrderBy(u => u.Id)
                .ToListAsync();

            await EnsureWorkShiftsAsync(context, now);
            await context.SaveChangesAsync();

            var shifts = await context.WorkShifts
                .Where(x => x.IsActive)
                .OrderBy(x => x.Id)
                .ToListAsync();

            await EnsureLeaveBalancesAsync(context, employees, now.Year, now);
            await EnsureWorkSchedulesAsync(context, employees, shifts, now, random);
            await context.SaveChangesAsync();

            await EnsureLeaveRequestsAsync(context, employees, reviewers, now, random);
            await context.SaveChangesAsync();

            await SyncLeaveBalancesAsync(context, employees.Select(e => e.Id).ToHashSet(), now.Year, now);
            await context.SaveChangesAsync();

            await EnsureAttendancesAsync(context, employees, now, random);
            await context.SaveChangesAsync();

            await EnsureAttendanceAdjustmentsAsync(context, reviewers, now, random);
            await EnsureOvertimeRequestsAsync(context, employees, reviewers, now, random);
            await context.SaveChangesAsync();

            await EnsurePayrollsAsync(context, employees, reviewers, now, random);
            await context.SaveChangesAsync();
        }

        private static async Task EnsureDepartmentsAsync(AppDbContext context)
        {
            var existing = await context.Departments
                .AsNoTracking()
                .Select(d => d.DepartmentCode)
                .ToListAsync();

            var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var toAdd = new List<Department>();

            for (var i = 0; i < DepartmentSeeds.Length; i++)
            {
                var code = $"DEP{(i + 1).ToString("00")}";
                if (existingSet.Contains(code)) continue;

                toAdd.Add(new Department
                {
                    DepartmentCode = code,
                    DepartmentName = DepartmentSeeds[i]
                });
            }

            if (toAdd.Count > 0)
                await context.Departments.AddRangeAsync(toAdd);
        }

        private static async Task EnsurePositionsAsync(AppDbContext context)
        {
            var existing = await context.Positions
                .AsNoTracking()
                .Select(p => p.PositionCode)
                .ToListAsync();

            var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var toAdd = new List<Position>();

            for (var i = 0; i < PositionSeeds.Length; i++)
            {
                var code = $"POS{(i + 1).ToString("00")}";
                if (existingSet.Contains(code)) continue;

                toAdd.Add(new Position
                {
                    PositionCode = code,
                    PositionName = PositionSeeds[i]
                });
            }

            if (toAdd.Count > 0)
                await context.Positions.AddRangeAsync(toAdd);
        }

        private static async Task EnsureEmployeesAsync(AppDbContext context, int targetEmployees, Random random, DateTime now)
        {
            var departments = await context.Departments.AsNoTracking().OrderBy(d => d.Id).ToListAsync();
            var positions = await context.Positions.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
            var currentCount = await context.Employees.CountAsync();
            if (currentCount >= targetEmployees || departments.Count == 0 || positions.Count == 0)
                return;

            var existingCodes = await context.Employees.AsNoTracking().Select(e => e.EmployeeCode).ToListAsync();
            var existingEmails = await context.Employees.AsNoTracking().Select(e => e.Email).ToListAsync();
            var codeSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var emailSet = existingEmails.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var nextSequence = GetNextEmployeeSequence(existingCodes);

            var toAdd = new List<Employee>();
            while (currentCount + toAdd.Count < targetEmployees)
            {
                var employeeCode = $"EMP{nextSequence.ToString("000")}";
                var firstName = FirstNames[(nextSequence - 1) % FirstNames.Length];
                var lastName = LastNames[(nextSequence - 1) % LastNames.Length];
                var fullName = $"{lastName} {firstName} {nextSequence.ToString("000")}";
                var email = $"employee{nextSequence.ToString("000")}@smarthr.local";

                if (codeSet.Contains(employeeCode) || emailSet.Contains(email))
                {
                    nextSequence++;
                    continue;
                }

                var department = departments[(nextSequence - 1) % departments.Count];
                var position = positions[(nextSequence - 1) % positions.Count];
                var salaryBand = 8500000m + ((nextSequence - 1) % 12) * 750000m + random.Next(0, 250000);

                toAdd.Add(new Employee
                {
                    EmployeeCode = employeeCode,
                    FullName = fullName,
                    Email = email,
                    DepartmentId = department.Id,
                    PositionId = position.Id,
                    BaseSalary = salaryBand,
                    HireDate = now.Date.AddDays(-random.Next(60, 900)),
                    IsActive = true
                });

                codeSet.Add(employeeCode);
                emailSet.Add(email);
                nextSequence++;
            }

            if (toAdd.Count > 0)
                await context.Employees.AddRangeAsync(toAdd);
        }

        private static async Task EnsureStandaloneRoleUsersAsync(AppDbContext context, PasswordService passwordService, string role, string baseUsername, string password, int targetCount)
        {
            var existingUsers = await context.AppUsers
                .Where(u => u.Role == role)
                .OrderBy(u => u.Id)
                .ToListAsync();

            if (existingUsers.Count >= targetCount)
                return;

            var usernameSet = (await context.AppUsers.AsNoTracking().Select(u => u.Username).ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var toAdd = new List<AppUser>();
            var sequence = 0;
            while (existingUsers.Count + toAdd.Count < targetCount)
            {
                var username = sequence == 0 ? baseUsername : $"{baseUsername}{sequence.ToString("00")}";
                sequence++;
                if (usernameSet.Contains(username)) continue;

                passwordService.CreatePasswordHash(password, out var hash, out var salt);
                toAdd.Add(new AppUser
                {
                    Username = username,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    Role = role,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                });
                usernameSet.Add(username);
            }

            if (toAdd.Count > 0)
                await context.AppUsers.AddRangeAsync(toAdd);
        }

        private static async Task EnsureLinkedRoleUsersAsync(
    AppDbContext context,
    PasswordService passwordService,
    string role,
    string baseUsername,
    string password,
    int targetCount)
        {
            var existingUsers = await context.AppUsers
                .Where(u => u.Role == role)
                .OrderBy(u => u.Id)
                .ToListAsync();

            if (existingUsers.Count >= targetCount)
                return;

            var usernameSet = (await context.AppUsers.AsNoTracking()
                .Select(u => u.Username)
                .ToListAsync())
                .Concat(context.AppUsers.Local.Select(u => u.Username))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var persistedLinkedEmployeeIds = await context.AppUsers
                .AsNoTracking()
                .Where(u => u.EmployeeId.HasValue)
                .Select(u => u.EmployeeId!.Value)
                .ToListAsync();

            var pendingLinkedEmployeeIds = context.AppUsers.Local
                .Where(u => u.EmployeeId.HasValue)
                .Select(u => u.EmployeeId!.Value);

            var linkedEmployeeIds = persistedLinkedEmployeeIds
                .Concat(pendingLinkedEmployeeIds)
                .ToHashSet();

            var availableEmployees = await context.Employees
                .Where(e => e.IsActive && !linkedEmployeeIds.Contains(e.Id))
                .OrderBy(e => e.Id)
                .ToListAsync();

            var toAdd = new List<AppUser>();
            var sequence = 0;
            var employeeIndex = 0;

            while (existingUsers.Count + toAdd.Count < targetCount && employeeIndex < availableEmployees.Count)
            {
                var username = sequence == 0
                    ? baseUsername
                    : $"{baseUsername}{sequence.ToString(role == UserRoles.Employee ? "000" : "00")}";

                sequence++;

                if (usernameSet.Contains(username))
                    continue;

                var employee = availableEmployees[employeeIndex++];

                passwordService.CreatePasswordHash(password, out var hash, out var salt);

                toAdd.Add(new AppUser
                {
                    Username = username,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    Role = role,
                    EmployeeId = employee.Id,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                });

                usernameSet.Add(username);
                linkedEmployeeIds.Add(employee.Id);
            }

            if (toAdd.Count > 0)
                await context.AppUsers.AddRangeAsync(toAdd);
        }

        private static async Task EnsureWorkShiftsAsync(AppDbContext context, DateTime now)
        {
            var desiredShifts = new[]
            {
                new WorkShift { ShiftCode = "SHIFT-AM", ShiftName = "Morning Shift", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(17, 0, 0), StandardHours = 8m, IsNightShift = false, IsActive = true, Notes = "Default office shift", CreatedAt = now },
                new WorkShift { ShiftCode = "SHIFT-FLX", ShiftName = "Flexible Shift", StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(18, 0, 0), StandardHours = 8m, IsNightShift = false, IsActive = true, Notes = "Flexible team shift", CreatedAt = now },
                new WorkShift { ShiftCode = "SHIFT-OPS", ShiftName = "Operations Shift", StartTime = new TimeSpan(7, 30, 0), EndTime = new TimeSpan(16, 30, 0), StandardHours = 8m, IsNightShift = false, IsActive = true, Notes = "Operations and logistics", CreatedAt = now },
                new WorkShift { ShiftCode = "SHIFT-EVE", ShiftName = "Evening Shift", StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(22, 0, 0), StandardHours = 8m, IsNightShift = false, IsActive = true, Notes = "Customer support evening coverage", CreatedAt = now },
                new WorkShift { ShiftCode = "SHIFT-NGT", ShiftName = "Night Shift", StartTime = new TimeSpan(22, 0, 0), EndTime = new TimeSpan(6, 0, 0), StandardHours = 8m, IsNightShift = true, IsActive = true, Notes = "Night support and monitoring", CreatedAt = now }
            };

            var existingCodes = (await context.WorkShifts.AsNoTracking().Select(x => x.ShiftCode).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var toAdd = desiredShifts.Where(x => !existingCodes.Contains(x.ShiftCode)).ToList();
            if (toAdd.Count > 0)
                await context.WorkShifts.AddRangeAsync(toAdd);
        }

        private static async Task EnsureLeaveBalancesAsync(AppDbContext context, List<Employee> employees, int year, DateTime now)
        {
            var existingEmployeeIds = (await context.LeaveBalances.AsNoTracking().Where(x => x.Year == year).Select(x => x.EmployeeId).ToListAsync()).ToHashSet();
            var toAdd = new List<LeaveBalance>();
            foreach (var employee in employees)
            {
                if (existingEmployeeIds.Contains(employee.Id)) continue;

                toAdd.Add(new LeaveBalance
                {
                    EmployeeId = employee.Id,
                    Year = year,
                    AnnualAllocated = 12,
                    AnnualUsed = 0,
                    SickAllocated = 6,
                    SickUsed = 0,
                    CarryForward = employee.HireDate.Year < year ? 2 : 0,
                    UnpaidDays = 0,
                    UpdatedAt = now
                });
            }

            if (toAdd.Count > 0)
                await context.LeaveBalances.AddRangeAsync(toAdd);
        }

        private static async Task EnsureWorkSchedulesAsync(AppDbContext context, List<Employee> employees, List<WorkShift> shifts, DateTime now, Random random)
        {
            if (shifts.Count == 0 || employees.Count == 0)
                return;

            var startDate = now.Date.AddDays(-21);
            var endDate = now.Date.AddDays(21);
            var existingPairs = (await context.WorkSchedules.AsNoTracking()
                .Where(x => x.WorkDate >= startDate && x.WorkDate <= endDate)
                .Select(x => new { x.EmployeeId, x.WorkDate })
                .ToListAsync())
                .Select(x => $"{x.EmployeeId}:{x.WorkDate:yyyyMMdd}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var toAdd = new List<WorkSchedule>();
            foreach (var employee in employees)
            {
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                        continue;

                    var key = $"{employee.Id}:{date:yyyyMMdd}";
                    if (existingPairs.Contains(key))
                        continue;

                    var shiftIndex = (employee.Id + date.Day) % shifts.Count;
                    if (employee.DepartmentId % 3 == 0 && shifts.Count > 1)
                        shiftIndex = (shiftIndex + 1) % shifts.Count;

                    toAdd.Add(new WorkSchedule
                    {
                        EmployeeId = employee.Id,
                        WorkShiftId = shifts[shiftIndex].Id,
                        WorkDate = date,
                        IsLocked = date < now.Date,
                        Notes = date.Date == now.Date ? "Today planned schedule" : (random.NextDouble() < 0.12 ? "Auto-seeded rotating shift" : null),
                        CreatedAt = now.AddMinutes(-random.Next(1000, 5000))
                    });
                }
            }

            if (toAdd.Count > 0)
                await context.WorkSchedules.AddRangeAsync(toAdd);
        }

        private static async Task EnsureLeaveRequestsAsync(AppDbContext context, List<Employee> employees, List<AppUser> reviewers, DateTime now, Random random)
        {
            if (employees.Count == 0)
                return;

            var existingCounts = await context.LeaveRequests
                .AsNoTracking()
                .GroupBy(x => x.EmployeeId)
                .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.EmployeeId, x => x.Count);

            var toAdd = new List<LeaveRequest>();
            foreach (var employee in employees)
            {
                var existingForEmployee = existingCounts.TryGetValue(employee.Id, out var count) ? count : 0;
                if (existingForEmployee >= 2)
                    continue;

                var needed = 2 - existingForEmployee;
                for (var i = 0; i < needed; i++)
                {
                    var offset = random.Next(5, 85);
                    var startDate = now.Date.AddDays(-offset);
                    while (startDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                        startDate = startDate.AddDays(-1);

                    var totalDays = random.Next(1, 4);
                    var endDate = startDate;
                    var counted = 1;
                    while (counted < totalDays)
                    {
                        endDate = endDate.AddDays(1);
                        if (endDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                            continue;
                        counted++;
                    }

                    var leaveType = ChooseLeaveType(random);
                    var statusRoll = random.NextDouble();
                    var status = statusRoll < 0.58 ? LeaveStatuses.Approved : statusRoll < 0.78 ? LeaveStatuses.Pending : statusRoll < 0.92 ? LeaveStatuses.Rejected : LeaveStatuses.Cancelled;
                    var reviewer = reviewers.Count == 0 ? null : reviewers[(employee.Id + i) % reviewers.Count];
                    var createdAt = startDate.AddDays(-random.Next(2, 9)).AddHours(9 + random.Next(0, 5));
                    var approvedAt = status == LeaveStatuses.Approved || status == LeaveStatuses.Rejected ? createdAt.AddHours(random.Next(4, 48)) : (DateTime?)null;

                    toAdd.Add(new LeaveRequest
                    {
                        EmployeeId = employee.Id,
                        LeaveType = leaveType,
                        StartDate = startDate,
                        EndDate = endDate,
                        TotalDays = totalDays,
                        Reason = BuildLeaveReason(leaveType, totalDays),
                        Status = status,
                        ApprovedByUserId = status == LeaveStatuses.Pending || status == LeaveStatuses.Cancelled ? null : reviewer?.Id,
                        ApprovedAt = approvedAt,
                        ApprovalNote = status == LeaveStatuses.Approved ? "Approved in seeded demo workflow." : null,
                        RejectionReason = status == LeaveStatuses.Rejected ? "Rejected in seeded demo workflow due to schedule overlap." : null,
                        CreatedAt = createdAt
                    });
                }
            }

            if (toAdd.Count == 0)
                return;

            await context.LeaveRequests.AddRangeAsync(toAdd);
            await context.SaveChangesAsync();

            var addedIds = toAdd.Select(x => x.Id).ToHashSet();
            var freshLeaveRequests = await context.LeaveRequests
                .Where(x => addedIds.Contains(x.Id))
                .OrderBy(x => x.Id)
                .ToListAsync();

            var auditLogs = new List<LeaveRequestAuditLog>();
            foreach (var request in freshLeaveRequests)
            {
                auditLogs.Add(new LeaveRequestAuditLog
                {
                    LeaveRequestId = request.Id,
                    PerformedByUserId = null,
                    ActionType = "Submitted",
                    PreviousStatus = null,
                    NewStatus = LeaveStatuses.Pending,
                    Note = request.Reason,
                    SnapshotJson = JsonSerializer.Serialize(new { request.LeaveType, request.StartDate, request.EndDate, request.TotalDays, request.Status }),
                    CreatedAt = request.CreatedAt
                });

                if (request.Status == LeaveStatuses.Approved)
                {
                    auditLogs.Add(new LeaveRequestAuditLog
                    {
                        LeaveRequestId = request.Id,
                        PerformedByUserId = request.ApprovedByUserId,
                        ActionType = "Approved",
                        PreviousStatus = LeaveStatuses.Pending,
                        NewStatus = LeaveStatuses.Approved,
                        Note = request.ApprovalNote,
                        SnapshotJson = JsonSerializer.Serialize(new { request.LeaveType, request.TotalDays, request.ApprovedAt }),
                        CreatedAt = request.ApprovedAt ?? request.CreatedAt.AddHours(8)
                    });
                }
                else if (request.Status == LeaveStatuses.Rejected)
                {
                    auditLogs.Add(new LeaveRequestAuditLog
                    {
                        LeaveRequestId = request.Id,
                        PerformedByUserId = request.ApprovedByUserId,
                        ActionType = "Rejected",
                        PreviousStatus = LeaveStatuses.Pending,
                        NewStatus = LeaveStatuses.Rejected,
                        Note = request.RejectionReason,
                        SnapshotJson = JsonSerializer.Serialize(new { request.LeaveType, request.TotalDays, request.ApprovedAt }),
                        CreatedAt = request.ApprovedAt ?? request.CreatedAt.AddHours(8)
                    });
                }
                else if (request.Status == LeaveStatuses.Cancelled)
                {
                    auditLogs.Add(new LeaveRequestAuditLog
                    {
                        LeaveRequestId = request.Id,
                        PerformedByUserId = null,
                        ActionType = "Cancelled",
                        PreviousStatus = LeaveStatuses.Pending,
                        NewStatus = LeaveStatuses.Cancelled,
                        Note = "Cancelled by seeded employee flow.",
                        SnapshotJson = JsonSerializer.Serialize(new { request.LeaveType, request.TotalDays }),
                        CreatedAt = request.CreatedAt.AddHours(12)
                    });
                }
            }

            await context.LeaveRequestAuditLogs.AddRangeAsync(auditLogs);
        }

        private static async Task SyncLeaveBalancesAsync(AppDbContext context, HashSet<int> employeeIds, int year, DateTime now)
        {
            var balances = await context.LeaveBalances
                .Where(x => x.Year == year && employeeIds.Contains(x.EmployeeId))
                .ToListAsync();

            var approvedLeaveRollups = await context.LeaveRequests
                .Where(x => employeeIds.Contains(x.EmployeeId)
                            && x.Status == LeaveStatuses.Approved
                            && x.StartDate.Year == year)
                .GroupBy(x => new { x.EmployeeId, x.LeaveType })
                .Select(g => new { g.Key.EmployeeId, g.Key.LeaveType, Total = g.Sum(x => x.TotalDays) })
                .ToListAsync();

            var lookup = approvedLeaveRollups
                .GroupBy(x => x.EmployeeId)
                .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.LeaveType, x => (decimal)x.Total));

            foreach (var balance in balances)
            {
                balance.AnnualUsed = 0;
                balance.SickUsed = 0;
                balance.UnpaidDays = 0;
                if (lookup.TryGetValue(balance.EmployeeId, out var totals))
                {
                    if (totals.TryGetValue(LeaveTypes.AnnualLeave, out var annual)) balance.AnnualUsed = annual;
                    if (totals.TryGetValue(LeaveTypes.SickLeave, out var sick)) balance.SickUsed = sick;
                    if (totals.TryGetValue(LeaveTypes.UnpaidLeave, out var unpaid)) balance.UnpaidDays = unpaid;
                    if (totals.TryGetValue(LeaveTypes.Other, out var other)) balance.UnpaidDays += other;
                }
                balance.UpdatedAt = now;
            }
        }

        private static async Task EnsureAttendancesAsync(AppDbContext context, List<Employee> employees, DateTime now, Random random)
        {
            if (employees.Count == 0)
                return;

            var startDate = now.Date.AddDays(-60);
            var endDate = now.Date.AddDays(-1);
            var existingPairs = (await context.Attendances.AsNoTracking()
                .Where(x => x.WorkDate >= startDate && x.WorkDate <= endDate)
                .Select(x => new { x.EmployeeId, x.WorkDate })
                .ToListAsync())
                .Select(x => $"{x.EmployeeId}:{x.WorkDate:yyyyMMdd}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var approvedLeaveDays = await context.LeaveRequests
                .AsNoTracking()
                .Where(x => x.Status == LeaveStatuses.Approved && x.EndDate >= startDate && x.StartDate <= endDate)
                .Select(x => new { x.EmployeeId, x.StartDate, x.EndDate })
                .ToListAsync();

            var leaveLookup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var leave in approvedLeaveDays)
            {
                for (var date = leave.StartDate.Date; date <= leave.EndDate.Date; date = date.AddDays(1))
                {
                    if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                        continue;
                    leaveLookup.Add($"{leave.EmployeeId}:{date:yyyyMMdd}");
                }
            }

            var toAdd = new List<Attendance>();
            foreach (var employee in employees)
            {
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                        continue;

                    var key = $"{employee.Id}:{date:yyyyMMdd}";
                    if (existingPairs.Contains(key))
                        continue;

                    if (leaveLookup.Contains(key))
                    {
                        toAdd.Add(new Attendance
                        {
                            EmployeeId = employee.Id,
                            WorkDate = date,
                            CheckInTime = date.AddHours(8),
                            CheckOutTime = null,
                            Status = AttendanceStatuses.Leave,
                            Note = "Auto-seeded from approved leave request.",
                            SourceType = AttendanceSourceTypes.ApprovedLeave,
                            SourceReferenceId = null
                        });
                        continue;
                    }

                    var roll = random.NextDouble();
                    var status = roll < 0.70 ? AttendanceStatuses.Present : roll < 0.82 ? AttendanceStatuses.Late : roll < 0.90 ? AttendanceStatuses.Remote : roll < 0.95 ? AttendanceStatuses.Absent : AttendanceStatuses.Present;
                    var checkIn = date.AddHours(8).AddMinutes(random.Next(-5, 8));
                    DateTime? checkOut = date.AddHours(17).AddMinutes(random.Next(-10, 31));
                    string? note;

                    if (status == AttendanceStatuses.Late)
                    {
                        checkIn = date.AddHours(8).AddMinutes(random.Next(15, 56));
                        note = "Seeded late check-in record.";
                    }
                    else if (status == AttendanceStatuses.Remote)
                    {
                        checkIn = date.AddHours(8).AddMinutes(random.Next(0, 25));
                        checkOut = date.AddHours(17).AddMinutes(random.Next(0, 20));
                        note = "Seeded remote work day.";
                    }
                    else if (status == AttendanceStatuses.Absent)
                    {
                        checkIn = date.AddHours(8);
                        checkOut = null;
                        note = "Seeded absent record awaiting follow-up.";
                    }
                    else
                    {
                        note = "Seeded normal attendance record.";
                    }

                    toAdd.Add(new Attendance
                    {
                        EmployeeId = employee.Id,
                        WorkDate = date,
                        CheckInTime = checkIn,
                        CheckOutTime = checkOut,
                        Status = status,
                        Note = note,
                        SourceType = AttendanceSourceTypes.Manual,
                        SourceReferenceId = null
                    });
                }
            }

            if (toAdd.Count > 0)
                await context.Attendances.AddRangeAsync(toAdd);
        }

        private static async Task EnsureAttendanceAdjustmentsAsync(AppDbContext context, List<AppUser> reviewers, DateTime now, Random random)
        {
            var existingEmployeeIds = (await context.AttendanceAdjustmentRequests.AsNoTracking().Select(x => x.EmployeeId).Distinct().ToListAsync()).ToHashSet();
            var candidateAttendances = await context.Attendances
                .Include(x => x.Employee)
                .Where(x => (x.Status == AttendanceStatuses.Late || x.Status == AttendanceStatuses.Absent)
                            && x.WorkDate >= now.Date.AddDays(-45)
                            && !existingEmployeeIds.Contains(x.EmployeeId))
                .OrderBy(x => x.EmployeeId)
                .ThenByDescending(x => x.WorkDate)
                .ToListAsync();

            var latestPerEmployee = candidateAttendances
                .GroupBy(x => x.EmployeeId)
                .Select(g => g.First())
                .Take(30)
                .ToList();

            if (latestPerEmployee.Count == 0)
                return;

            var requests = new List<AttendanceAdjustmentRequest>();
            foreach (var attendance in latestPerEmployee)
            {
                var reviewRoll = random.NextDouble();
                var status = reviewRoll < 0.50 ? AttendanceAdjustmentStatuses.Approved : reviewRoll < 0.80 ? AttendanceAdjustmentStatuses.Pending : AttendanceAdjustmentStatuses.Rejected;
                var reviewer = reviewers.Count == 0 ? null : reviewers[(attendance.EmployeeId + attendance.WorkDate.Day) % reviewers.Count];
                var requestedStatus = attendance.Status == AttendanceStatuses.Absent ? AttendanceStatuses.Present : AttendanceStatuses.Present;
                var requestedCheckIn = attendance.WorkDate.Date.AddHours(8).AddMinutes(random.Next(0, 12));
                var requestedCheckOut = attendance.WorkDate.Date.AddHours(17).AddMinutes(random.Next(0, 20));
                var createdAt = attendance.WorkDate.Date.AddDays(1).AddHours(9 + random.Next(0, 3));

                requests.Add(new AttendanceAdjustmentRequest
                {
                    EmployeeId = attendance.EmployeeId,
                    AttendanceId = attendance.Id,
                    WorkDate = attendance.WorkDate.Date,
                    RequestedCheckInTime = requestedCheckIn,
                    RequestedCheckOutTime = requestedCheckOut,
                    RequestedStatus = requestedStatus,
                    Reason = attendance.Status == AttendanceStatuses.Absent
                        ? "Seeded adjustment request to recover missed punch due to client visit."
                        : "Seeded adjustment request to correct late check-in due to traffic.",
                    Status = status,
                    CreatedAt = createdAt,
                    ReviewedByUserId = status == AttendanceAdjustmentStatuses.Pending ? null : reviewer?.Id,
                    ReviewedAt = status == AttendanceAdjustmentStatuses.Pending ? null : createdAt.AddHours(random.Next(4, 30)),
                    ReviewNote = status == AttendanceAdjustmentStatuses.Approved
                        ? "Approved in seeded workflow."
                        : status == AttendanceAdjustmentStatuses.Rejected
                            ? "Rejected in seeded workflow because supporting note was insufficient."
                            : null
                });
            }

            await context.AttendanceAdjustmentRequests.AddRangeAsync(requests);
            await context.SaveChangesAsync();

            var requestIds = requests.Select(x => x.Id).ToHashSet();
            var freshRequests = await context.AttendanceAdjustmentRequests
                .Include(x => x.Employee)
                .Where(x => requestIds.Contains(x.Id))
                .ToListAsync();

            var auditLogs = new List<AttendanceAdjustmentAuditLog>();
            foreach (var request in freshRequests)
            {
                auditLogs.Add(new AttendanceAdjustmentAuditLog
                {
                    AttendanceAdjustmentRequestId = request.Id,
                    EmployeeId = request.EmployeeId,
                    EmployeeCode = request.Employee?.EmployeeCode ?? string.Empty,
                    EmployeeFullName = request.Employee?.FullName ?? string.Empty,
                    WorkDate = request.WorkDate,
                    RequestedStatus = request.RequestedStatus,
                    CurrentStatus = request.Status,
                    PerformedByUserId = null,
                    ActionType = "Submitted",
                    PreviousStatus = null,
                    NewStatus = AttendanceAdjustmentStatuses.Pending,
                    Note = request.Reason,
                    SnapshotJson = JsonSerializer.Serialize(new { request.RequestedStatus, request.RequestedCheckInTime, request.RequestedCheckOutTime }),
                    CreatedAt = request.CreatedAt
                });

                if (request.Status != AttendanceAdjustmentStatuses.Pending)
                {
                    auditLogs.Add(new AttendanceAdjustmentAuditLog
                    {
                        AttendanceAdjustmentRequestId = request.Id,
                        EmployeeId = request.EmployeeId,
                        EmployeeCode = request.Employee?.EmployeeCode ?? string.Empty,
                        EmployeeFullName = request.Employee?.FullName ?? string.Empty,
                        WorkDate = request.WorkDate,
                        RequestedStatus = request.RequestedStatus,
                        CurrentStatus = request.Status,
                        PerformedByUserId = request.ReviewedByUserId,
                        ActionType = request.Status == AttendanceAdjustmentStatuses.Approved ? "Approved" : "Rejected",
                        PreviousStatus = AttendanceAdjustmentStatuses.Pending,
                        NewStatus = request.Status,
                        Note = request.ReviewNote,
                        SnapshotJson = JsonSerializer.Serialize(new { request.Status, request.ReviewedAt }),
                        CreatedAt = request.ReviewedAt ?? request.CreatedAt.AddHours(8)
                    });
                }
            }

            await context.AttendanceAdjustmentAuditLogs.AddRangeAsync(auditLogs);
        }

        private static async Task EnsureOvertimeRequestsAsync(AppDbContext context, List<Employee> employees, List<AppUser> reviewers, DateTime now, Random random)
        {
            if (employees.Count == 0)
                return;

            var existingEmployeeIds = (await context.OvertimeRequests.AsNoTracking().Select(x => x.EmployeeId).Distinct().ToListAsync()).ToHashSet();
            var toAdd = new List<OvertimeRequest>();
            foreach (var employee in employees.Where(e => !existingEmployeeIds.Contains(e.Id)).Take(45))
            {
                var workDate = now.Date.AddDays(-random.Next(3, 55));
                while (workDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                    workDate = workDate.AddDays(-1);

                var start = workDate.AddHours(18);
                var hours = Math.Round((decimal)(1.5 + random.NextDouble() * 2.5), 1);
                var end = start.AddHours((double)hours);
                var statusRoll = random.NextDouble();
                var status = statusRoll < 0.55 ? OvertimeStatuses.Approved : statusRoll < 0.82 ? OvertimeStatuses.Pending : statusRoll < 0.95 ? OvertimeStatuses.Rejected : OvertimeStatuses.Cancelled;
                var reviewer = reviewers.Count == 0 ? null : reviewers[(employee.Id + workDate.Day) % reviewers.Count];
                var createdAt = workDate.AddDays(-1).AddHours(16);

                toAdd.Add(new OvertimeRequest
                {
                    EmployeeId = employee.Id,
                    WorkDate = workDate,
                    StartTime = start,
                    EndTime = end,
                    Hours = hours,
                    Reason = "Seeded overtime for month-end workload and client support.",
                    Status = status,
                    ApprovedByUserId = status == OvertimeStatuses.Pending || status == OvertimeStatuses.Cancelled ? null : reviewer?.Id,
                    ApprovedAt = status == OvertimeStatuses.Approved || status == OvertimeStatuses.Rejected ? createdAt.AddHours(random.Next(2, 20)) : null,
                    ApprovalNote = status == OvertimeStatuses.Approved ? "Approved for seeded payroll demo." : null,
                    RejectionReason = status == OvertimeStatuses.Rejected ? "Rejected in seeded flow because budget limit was reached." : null,
                    AppliedToPayroll = false,
                    CreatedAt = createdAt
                });
            }

            if (toAdd.Count > 0)
                await context.OvertimeRequests.AddRangeAsync(toAdd);
        }

        private static async Task EnsurePayrollsAsync(AppDbContext context, List<Employee> employees, List<AppUser> reviewers, DateTime now, Random random)
        {
            if (employees.Count == 0)
                return;

            var months = new[]
            {
                new DateTime(now.Year, now.Month, 1).AddMonths(-3),
                new DateTime(now.Year, now.Month, 1).AddMonths(-2),
                new DateTime(now.Year, now.Month, 1).AddMonths(-1)
            };

            var existingKeys = (await context.Payrolls.AsNoTracking()
                .Select(x => new { x.EmployeeId, x.PayrollMonth, x.PayrollYear })
                .ToListAsync())
                .Select(x => $"{x.EmployeeId}:{x.PayrollYear}:{x.PayrollMonth}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var attendances = await context.Attendances.AsNoTracking()
                .Where(x => x.WorkDate >= months.Min(x => x) && x.WorkDate < new DateTime(now.Year, now.Month, 1))
                .ToListAsync();
            var approvedOvertimes = await context.OvertimeRequests.AsNoTracking()
                .Where(x => x.Status == OvertimeStatuses.Approved && x.WorkDate >= months.Min(x => x) && x.WorkDate < new DateTime(now.Year, now.Month, 1))
                .ToListAsync();
            var approvedLeaves = await context.LeaveRequests.AsNoTracking()
                .Where(x => x.Status == LeaveStatuses.Approved && x.EndDate >= months.Min(x => x) && x.StartDate < new DateTime(now.Year, now.Month, 1))
                .ToListAsync();

            var toAdd = new List<Payroll>();
            foreach (var month in months)
            {
                foreach (var employee in employees)
                {
                    var key = $"{employee.Id}:{month.Year}:{month.Month}";
                    if (existingKeys.Contains(key))
                        continue;

                    var monthAttendances = attendances.Where(x => x.EmployeeId == employee.Id && x.WorkDate.Year == month.Year && x.WorkDate.Month == month.Month).ToList();
                    var presentDays = monthAttendances.Count(x => x.Status == AttendanceStatuses.Present);
                    var lateDays = monthAttendances.Count(x => x.Status == AttendanceStatuses.Late);
                    var remoteDays = monthAttendances.Count(x => x.Status == AttendanceStatuses.Remote);
                    var absentDays = monthAttendances.Count(x => x.Status == AttendanceStatuses.Absent);
                    var leaveDays = monthAttendances.Count(x => x.Status == AttendanceStatuses.Leave);
                    var effectiveWorkingDays = presentDays + lateDays + remoteDays + leaveDays;

                    var monthLeaves = approvedLeaves.Where(x => x.EmployeeId == employee.Id && x.StartDate.Year <= month.Year && x.EndDate.Year >= month.Year && x.StartDate.Month <= month.Month && x.EndDate.Month >= month.Month).ToList();
                    var paidLeaveDays = monthLeaves.Where(x => x.LeaveType == LeaveTypes.AnnualLeave || x.LeaveType == LeaveTypes.SickLeave).Sum(x => (decimal)x.TotalDays);
                    var unpaidLeaveDays = monthLeaves.Where(x => x.LeaveType == LeaveTypes.UnpaidLeave || x.LeaveType == LeaveTypes.Other).Sum(x => (decimal)x.TotalDays);

                    var monthOt = approvedOvertimes.Where(x => x.EmployeeId == employee.Id && x.WorkDate.Year == month.Year && x.WorkDate.Month == month.Month).ToList();
                    var overtimeHours = monthOt.Sum(x => x.Hours);
                    var approvedOtCount = monthOt.Count;

                    var dailySalary = Math.Round(employee.BaseSalary / 26m, 2);
                    var overtimeHourly = Math.Round((employee.BaseSalary / 26m) / 8m, 2);
                    var overtimePay = Math.Round(overtimeHours * overtimeHourly * 1.5m, 2);
                    var bonus = Math.Round(150000m + (presentDays + remoteDays) * 12000m + overtimeHours * 25000m + random.Next(0, 120000), 2);
                    var deduction = Math.Round(absentDays * dailySalary + lateDays * 45000m + unpaidLeaveDays * dailySalary, 2);
                    var earnedBase = Math.Round(dailySalary * Math.Max(effectiveWorkingDays - absentDays, 0), 2);
                    var netSalary = Math.Max(earnedBase + bonus + overtimePay - deduction, 0);

                    toAdd.Add(new Payroll
                    {
                        EmployeeId = employee.Id,
                        PayrollMonth = month.Month,
                        PayrollYear = month.Year,
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
                        ApprovedOvertimeRequests = approvedOtCount,
                        Bonus = bonus,
                        Deduction = deduction,
                        OvertimePay = overtimePay,
                        NetSalary = netSalary,
                        GeneratedAt = month.AddMonths(1).AddDays(1).AddHours(9)
                    });
                }
            }

            if (toAdd.Count == 0)
                return;

            await context.Payrolls.AddRangeAsync(toAdd);
            await context.SaveChangesAsync();

            var payrollIds = toAdd.Select(x => x.Id).ToHashSet();
            var createdPayrolls = await context.Payrolls
                .Include(x => x.Employee)
                .Where(x => payrollIds.Contains(x.Id))
                .ToListAsync();

            var actor = reviewers.FirstOrDefault();
            var auditLogs = createdPayrolls.Select(payroll => new PayrollAuditLog
            {
                PayrollId = payroll.Id,
                EmployeeId = payroll.EmployeeId,
                EmployeeCode = payroll.Employee?.EmployeeCode ?? string.Empty,
                EmployeeFullName = payroll.Employee?.FullName ?? string.Empty,
                PayrollMonth = payroll.PayrollMonth,
                PayrollYear = payroll.PayrollYear,
                BaseSalary = payroll.BaseSalary,
                Bonus = payroll.Bonus,
                Deduction = payroll.Deduction,
                NetSalary = payroll.NetSalary,
                PerformedByUserId = actor?.Id,
                ActionType = "Generated",
                Note = "Seeded payroll run for dashboard and reporting demo.",
                SnapshotJson = JsonSerializer.Serialize(new { payroll.PresentDays, payroll.LateDays, payroll.AbsentDays, payroll.LeaveDays, payroll.OvertimeHours, payroll.OvertimePay }),
                CreatedAt = payroll.GeneratedAt
            }).ToList();

            await context.PayrollAuditLogs.AddRangeAsync(auditLogs);

            var overtimeIdsToMark = approvedOvertimes
                .Where(x => months.Any(m => m.Year == x.WorkDate.Year && m.Month == x.WorkDate.Month))
                .Select(x => x.Id)
                .ToHashSet();

            var overtimeToUpdate = await context.OvertimeRequests
                .Where(x => overtimeIdsToMark.Contains(x.Id) && !x.AppliedToPayroll)
                .ToListAsync();

            foreach (var overtime in overtimeToUpdate)
                overtime.AppliedToPayroll = true;
        }

        private static int GetNextEmployeeSequence(IEnumerable<string> codes)
        {
            var max = 0;
            foreach (var code in codes)
            {
                var digits = new string(code.Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, out var value) && value > max)
                    max = value;
            }
            return max + 1;
        }

        private static string ChooseLeaveType(Random random)
        {
            var roll = random.NextDouble();
            if (roll < 0.58) return LeaveTypes.AnnualLeave;
            if (roll < 0.78) return LeaveTypes.SickLeave;
            if (roll < 0.93) return LeaveTypes.UnpaidLeave;
            return LeaveTypes.Other;
        }

        private static string BuildLeaveReason(string leaveType, int totalDays)
        {
            return leaveType switch
            {
                var x when x == LeaveTypes.AnnualLeave => totalDays > 1 ? "Annual leave for personal travel and family plan." : "Annual leave for personal errand.",
                var x when x == LeaveTypes.SickLeave => "Sick leave seeded for medical recovery and checkup.",
                var x when x == LeaveTypes.UnpaidLeave => "Unpaid leave seeded for personal commitment.",
                _ => "Other leave seeded for special personal arrangement."
            };
        }
    }
}
