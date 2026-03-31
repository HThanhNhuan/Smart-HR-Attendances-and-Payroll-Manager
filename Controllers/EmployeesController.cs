using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using smart_hr_attendance_payroll_management.Common;
using smart_hr_attendance_payroll_management.Data;
using smart_hr_attendance_payroll_management.DTOs;
using smart_hr_attendance_payroll_management.Entities;

namespace smart_hr_attendance_payroll_management.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class EmployeesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly Services.PasswordService _passwordService;

        public EmployeesController(AppDbContext context, Services.PasswordService passwordService)
        {
            _context = context;
            _passwordService = passwordService;
        }

        [HttpGet]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetAll()
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Select(e => new
                {
                    e.Id,
                    e.EmployeeCode,
                    e.FullName,
                    e.Email,
                    e.BaseSalary,
                    e.HireDate,
                    e.IsActive,
                    e.DepartmentId,
                    DepartmentName = e.Department != null ? e.Department.DepartmentName : null,
                    e.PositionId,
                    PositionName = e.Position != null ? e.Position.PositionName : null,
                    HasLoginAccount = _context.AppUsers.Any(u => u.EmployeeId == e.Id),
                    Username = _context.AppUsers.Where(u => u.EmployeeId == e.Id).Select(u => u.Username).FirstOrDefault(),
                    AccountRole = _context.AppUsers.Where(u => u.EmployeeId == e.Id).Select(u => u.Role).FirstOrDefault(),
                    AccountIsActive = _context.AppUsers.Where(u => u.EmployeeId == e.Id).Select(u => (bool?)u.IsActive).FirstOrDefault() ?? false
                })
                .ToListAsync();

            return Ok(employees);
        }

        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,HR,Manager")]
        public async Task<IActionResult> GetById(int id)
        {
            var employee = await _context.Employees
                .AsNoTracking()
                .Where(e => e.Id == id)
                .Select(e => new
                {
                    e.Id,
                    e.EmployeeCode,
                    e.FullName,
                    e.Email,
                    e.BaseSalary,
                    e.HireDate,
                    e.IsActive,
                    e.DepartmentId,
                    DepartmentName = e.Department != null ? e.Department.DepartmentName : null,
                    e.PositionId,
                    PositionName = e.Position != null ? e.Position.PositionName : null,
                    HasLoginAccount = _context.AppUsers.Any(u => u.EmployeeId == e.Id),
                    Username = _context.AppUsers.Where(u => u.EmployeeId == e.Id).Select(u => u.Username).FirstOrDefault(),
                    AccountRole = _context.AppUsers.Where(u => u.EmployeeId == e.Id).Select(u => u.Role).FirstOrDefault(),
                    AccountIsActive = _context.AppUsers.Where(u => u.EmployeeId == e.Id).Select(u => (bool?)u.IsActive).FirstOrDefault() ?? false
                })
                .FirstOrDefaultAsync();

            if (employee == null)
                return NotFound();

            return Ok(employee);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> Create(CreateEmployeeRequest request)
        {
            var requestedRole = NormalizeLinkedRole(request.AccountRole);
            if (request.CreateLoginAccount && (requestedRole == UserRoles.HR || requestedRole == UserRoles.Manager) && !User.IsInRole(UserRoles.Admin))
                return Forbid();

            var employeeCode = request.EmployeeCode.Trim();
            var fullName = request.FullName.Trim();
            var email = request.Email.Trim();
            var normalizedEmail = email.ToLower();

            var department = await _context.Departments.FindAsync(request.DepartmentId);
            if (department == null)
                return BadRequest("DepartmentId does not exist.");

            var position = await _context.Positions.FindAsync(request.PositionId);
            if (position == null)
                return BadRequest("PositionId does not exist.");

            var employeeCodeExists = await _context.Employees
                .AnyAsync(e => e.EmployeeCode == employeeCode);

            if (employeeCodeExists)
                return BadRequest("EmployeeCode already exists.");

            var emailExists = await _context.Employees
                .AnyAsync(e => e.Email.ToLower() == normalizedEmail);

            if (emailExists)
                return BadRequest("Email already exists.");

            var employee = new Employee
            {
                EmployeeCode = employeeCode,
                FullName = fullName,
                Email = email,
                BaseSalary = request.BaseSalary,
                HireDate = request.HireDate,
                IsActive = request.IsActive,
                DepartmentId = request.DepartmentId,
                PositionId = request.PositionId
            };

            AppUser? linkedUser = null;

            if (request.CreateLoginAccount)
            {
                var validationResult = await BuildLinkedUserAsync(
                    request.Username,
                    request.Password,
                    request.AccountRole,
                    request.IsActive,
                    employee.Id,
                    creatingNewUser: true);

                if (validationResult.Error != null)
                    return BadRequest(validationResult.Error);

                linkedUser = validationResult.User;
                if (linkedUser != null)
                {
                    linkedUser.Employee = employee;
                    _context.AppUsers.Add(linkedUser);
                }
            }

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = employee.Id }, CreateEmployeeResponse(employee, department.DepartmentName, position.PositionName, linkedUser));
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> Update(int id, UpdateEmployeeRequest request)
        {
            var requestedRole = NormalizeLinkedRole(request.AccountRole);
            if (request.HasLoginAccount && (requestedRole == UserRoles.HR || requestedRole == UserRoles.Manager) && !User.IsInRole(UserRoles.Admin))
                return Forbid();

            var employee = await _context.Employees.FindAsync(id);

            if (employee == null)
                return NotFound();

            var linkedUser = await _context.AppUsers.FirstOrDefaultAsync(u => u.EmployeeId == id);
            var employeeCode = request.EmployeeCode.Trim();
            var fullName = request.FullName.Trim();
            var email = request.Email.Trim();
            var normalizedEmail = email.ToLower();

            var department = await _context.Departments.FindAsync(request.DepartmentId);
            if (department == null)
                return BadRequest("DepartmentId does not exist.");

            var position = await _context.Positions.FindAsync(request.PositionId);
            if (position == null)
                return BadRequest("PositionId does not exist.");

            var employeeCodeExists = await _context.Employees
                .AnyAsync(e => e.Id != id && e.EmployeeCode == employeeCode);

            if (employeeCodeExists)
                return BadRequest("EmployeeCode already exists.");

            var emailExists = await _context.Employees
                .AnyAsync(e => e.Id != id && e.Email.ToLower() == normalizedEmail);

            if (emailExists)
                return BadRequest("Email already exists.");

            employee.EmployeeCode = employeeCode;
            employee.FullName = fullName;
            employee.Email = email;
            employee.BaseSalary = request.BaseSalary;
            employee.HireDate = request.HireDate;
            employee.IsActive = request.IsActive;
            employee.DepartmentId = request.DepartmentId;
            employee.PositionId = request.PositionId;

            if (request.HasLoginAccount)
            {
                if (linkedUser == null)
                {
                    var newUserResult = await BuildLinkedUserAsync(
                        request.Username,
                        request.NewPassword,
                        request.AccountRole,
                        request.IsActive,
                        employee.Id,
                        creatingNewUser: true);

                    if (newUserResult.Error != null)
                        return BadRequest(newUserResult.Error);

                    linkedUser = newUserResult.User;
                    if (linkedUser != null)
                    {
                        linkedUser.EmployeeId = employee.Id;
                        _context.AppUsers.Add(linkedUser);
                    }
                }
                else
                {
                    var normalizedRole = NormalizeLinkedRole(request.AccountRole);
                    if (normalizedRole == null)
                        return BadRequest("AccountRole must be Employee, HR, or Manager.");

                    var username = request.Username?.Trim();
                    if (string.IsNullOrWhiteSpace(username))
                        return BadRequest("Username is required when the employee has a login account.");

                    var usernameExists = await _context.AppUsers
                        .AnyAsync(u => u.Id != linkedUser.Id && u.Username == username);

                    if (usernameExists)
                        return BadRequest("Username already exists.");

                    linkedUser.Username = username;
                    linkedUser.Role = normalizedRole;
                    linkedUser.IsActive = request.IsActive;

                    if (!string.IsNullOrWhiteSpace(request.NewPassword))
                    {
                        _passwordService.CreatePasswordHash(
                            request.NewPassword.Trim(),
                            out byte[] passwordHash,
                            out byte[] passwordSalt);

                        linkedUser.PasswordHash = passwordHash;
                        linkedUser.PasswordSalt = passwordSalt;
                    }
                }
            }
            else if (linkedUser != null)
            {
                _context.AppUsers.Remove(linkedUser);
                linkedUser = null;
            }

            await _context.SaveChangesAsync();

            return Ok(CreateEmployeeResponse(employee, department.DepartmentName, position.PositionName, linkedUser));
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var employee = await _context.Employees.FindAsync(id);

            if (employee == null)
                return NotFound();

            var linkedUser = await _context.AppUsers.FirstOrDefaultAsync(u => u.EmployeeId == id);
            if (linkedUser != null)
            {
                _context.AppUsers.Remove(linkedUser);
            }

            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("my-profile")]
        [Authorize(Roles = "Employee,Manager")]
        public async Task<IActionResult> GetMyProfile()
        {
            var employeeIdClaim = User.FindFirst("employeeId")?.Value;

            if (string.IsNullOrWhiteSpace(employeeIdClaim))
                return Forbid();

            if (!int.TryParse(employeeIdClaim, out var employeeId))
                return Forbid();

            var employee = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Include(e => e.Position)
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee == null)
                return NotFound("Employee profile not found.");

            var response = new EmployeeProfileResponse
            {
                Id = employee.Id,
                EmployeeCode = employee.EmployeeCode,
                FullName = employee.FullName,
                Email = employee.Email,
                BaseSalary = employee.BaseSalary,
                HireDate = employee.HireDate,
                IsActive = employee.IsActive,
                DepartmentId = employee.DepartmentId,
                DepartmentName = employee.Department?.DepartmentName ?? string.Empty,
                PositionId = employee.PositionId,
                PositionName = employee.Position?.PositionName ?? string.Empty
            };

            return Ok(response);
        }

        [HttpPut("my-profile")]
        [Authorize(Roles = "Employee,Manager")]
        public async Task<IActionResult> UpdateMyProfile(UpdateMyProfileRequest request)
        {
            var employeeIdClaim = User.FindFirst("employeeId")?.Value;

            if (string.IsNullOrWhiteSpace(employeeIdClaim))
                return Forbid();

            if (!int.TryParse(employeeIdClaim, out var employeeId))
                return Forbid();

            var employee = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Position)
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee == null)
                return NotFound("Employee profile not found.");

            var fullName = request.FullName.Trim();
            var email = request.Email.Trim();
            var normalizedEmail = email.ToLower();

            var emailExists = await _context.Employees
                .AnyAsync(e => e.Id != employeeId && e.Email.ToLower() == normalizedEmail);

            if (emailExists)
                return BadRequest("Email already exists.");

            employee.FullName = fullName;
            employee.Email = email;

            await _context.SaveChangesAsync();

            var response = new EmployeeProfileResponse
            {
                Id = employee.Id,
                EmployeeCode = employee.EmployeeCode,
                FullName = employee.FullName,
                Email = employee.Email,
                BaseSalary = employee.BaseSalary,
                HireDate = employee.HireDate,
                IsActive = employee.IsActive,
                DepartmentId = employee.DepartmentId,
                DepartmentName = employee.Department?.DepartmentName ?? string.Empty,
                PositionId = employee.PositionId,
                PositionName = employee.Position?.PositionName ?? string.Empty
            };

            return Ok(response);
        }

        private async Task<(AppUser? User, string? Error)> BuildLinkedUserAsync(
            string? usernameInput,
            string? passwordInput,
            string? roleInput,
            bool isActive,
            int employeeId,
            bool creatingNewUser)
        {
            var normalizedRole = NormalizeLinkedRole(roleInput);
            if (normalizedRole == null)
                return (null, "AccountRole must be Employee, HR, or Manager.");

            var username = usernameInput?.Trim();
            if (string.IsNullOrWhiteSpace(username))
                return (null, "Username is required when creating a login account.");

            var usernameExists = await _context.AppUsers.AnyAsync(u => u.Username == username);
            if (usernameExists)
                return (null, "Username already exists.");

            var password = passwordInput?.Trim();
            if (creatingNewUser && string.IsNullOrWhiteSpace(password))
                return (null, "Password is required when creating a login account.");

            _passwordService.CreatePasswordHash(
                password!,
                out byte[] passwordHash,
                out byte[] passwordSalt);

            return (new AppUser
            {
                Username = username,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                Role = normalizedRole,
                EmployeeId = employeeId,
                IsActive = isActive,
                CreatedAt = DateTime.Now
            }, null);
        }

        private static string? NormalizeLinkedRole(string? role)
        {
            var value = role?.Trim();
            if (string.IsNullOrWhiteSpace(value))
                return UserRoles.Employee;

            if (string.Equals(value, UserRoles.Employee, StringComparison.OrdinalIgnoreCase))
                return UserRoles.Employee;

            if (string.Equals(value, UserRoles.HR, StringComparison.OrdinalIgnoreCase))
                return UserRoles.HR;

            if (string.Equals(value, UserRoles.Manager, StringComparison.OrdinalIgnoreCase))
                return UserRoles.Manager;

            return null;
        }

        private static object CreateEmployeeResponse(Employee employee, string? departmentName, string? positionName, AppUser? linkedUser)
        {
            return new
            {
                employee.Id,
                employee.EmployeeCode,
                employee.FullName,
                employee.Email,
                employee.BaseSalary,
                employee.HireDate,
                employee.IsActive,
                employee.DepartmentId,
                DepartmentName = departmentName,
                employee.PositionId,
                PositionName = positionName,
                HasLoginAccount = linkedUser != null,
                Username = linkedUser?.Username,
                AccountRole = linkedUser?.Role,
                AccountIsActive = linkedUser?.IsActive ?? false
            };
        }
    }
}
