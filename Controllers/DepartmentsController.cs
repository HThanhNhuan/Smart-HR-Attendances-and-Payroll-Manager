using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using smart_hr_attendance_payroll_management.Data;
using smart_hr_attendance_payroll_management.DTOs;
using smart_hr_attendance_payroll_management.Entities;
using Microsoft.AspNetCore.Authorization;

namespace smart_hr_attendance_payroll_management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DepartmentsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DepartmentsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> GetAll()
        {
            var departments = await _context.Departments
                .AsNoTracking()
                .Select(d => new
                {
                    d.Id,
                    d.DepartmentCode,
                    d.DepartmentName,
                    EmployeeCount = d.Employees.Count
                })
                .ToListAsync();

            return Ok(departments);
        }

        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> GetById(int id)
        {
            var department = await _context.Departments
                .AsNoTracking()
                .Where(d => d.Id == id)
                .Select(d => new
                {
                    d.Id,
                    d.DepartmentCode,
                    d.DepartmentName,
                    Employees = d.Employees.Select(e => new
                    {
                        e.Id,
                        e.EmployeeCode,
                        e.FullName,
                        e.Email,
                        e.BaseSalary,
                        e.HireDate,
                        e.IsActive,
                        e.DepartmentId
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (department == null)
                return NotFound();

            return Ok(department);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(CreateDepartmentRequest request)
        {
            var departmentCode = request.DepartmentCode.Trim();
            var departmentName = request.DepartmentName.Trim();

            var departmentCodeExists = await _context.Departments
                .AnyAsync(d => d.DepartmentCode == departmentCode);

            if (departmentCodeExists)
                return BadRequest("DepartmentCode already exists.");

            var department = new Department
            {
                DepartmentCode = departmentCode,
                DepartmentName = departmentName
            };

            _context.Departments.Add(department);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = department.Id }, new
            {
                department.Id,
                department.DepartmentCode,
                department.DepartmentName
            });
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, UpdateDepartmentRequest request)
        {
            var department = await _context.Departments.FindAsync(id);

            if (department == null)
                return NotFound();

            var departmentCode = request.DepartmentCode.Trim();
            var departmentName = request.DepartmentName.Trim();

            var departmentCodeExists = await _context.Departments
                .AnyAsync(d => d.Id != id && d.DepartmentCode == departmentCode);

            if (departmentCodeExists)
                return BadRequest("DepartmentCode already exists.");

            department.DepartmentCode = departmentCode;
            department.DepartmentName = departmentName;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                department.Id,
                department.DepartmentCode,
                department.DepartmentName
            });
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var department = await _context.Departments
                .Include(d => d.Employees)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (department == null)
                return NotFound();

            if (department.Employees.Any())
                return BadRequest("Cannot delete this department because it still has employees.");

            _context.Departments.Remove(department);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}