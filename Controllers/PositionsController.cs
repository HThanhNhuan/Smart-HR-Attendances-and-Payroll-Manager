using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using smart_hr_attendance_payroll_management.Data;
using smart_hr_attendance_payroll_management.DTOs;
using smart_hr_attendance_payroll_management.Entities;
using Microsoft.AspNetCore.Authorization;

namespace smart_hr_attendance_payroll_management.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class PositionsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PositionsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> GetAll()
        {
            var positions = await _context.Positions
                .AsNoTracking()
                .Select(p => new
                {
                    p.Id,
                    p.PositionCode,
                    p.PositionName,
                    EmployeeCount = p.Employees.Count
                })
                .ToListAsync();

            return Ok(positions);
        }

        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> GetById(int id)
        {
            var position = await _context.Positions
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    p.Id,
                    p.PositionCode,
                    p.PositionName,
                    Employees = p.Employees.Select(e => new
                    {
                        e.Id,
                        e.EmployeeCode,
                        e.FullName,
                        e.Email,
                        e.BaseSalary,
                        e.HireDate,
                        e.IsActive,
                        e.DepartmentId,
                        e.PositionId
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (position == null)
                return NotFound();

            return Ok(position);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(CreatePositionRequest request)
        {
            var positionCode = request.PositionCode.Trim();
            var positionName = request.PositionName.Trim();

            var positionCodeExists = await _context.Positions
                .AnyAsync(p => p.PositionCode == positionCode);

            if (positionCodeExists)
                return BadRequest("PositionCode already exists.");

            var position = new Position
            {
                PositionCode = positionCode,
                PositionName = positionName
            };

            _context.Positions.Add(position);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = position.Id }, new
            {
                position.Id,
                position.PositionCode,
                position.PositionName
            });
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, UpdatePositionRequest request)
        {
            var position = await _context.Positions.FindAsync(id);

            if (position == null)
                return NotFound();

            var positionCode = request.PositionCode.Trim();
            var positionName = request.PositionName.Trim();

            var positionCodeExists = await _context.Positions
                .AnyAsync(p => p.Id != id && p.PositionCode == positionCode);

            if (positionCodeExists)
                return BadRequest("PositionCode already exists.");

            position.PositionCode = positionCode;
            position.PositionName = positionName;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                position.Id,
                position.PositionCode,
                position.PositionName
            });
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var position = await _context.Positions
                .Include(p => p.Employees)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (position == null)
                return NotFound();

            if (position.Employees.Any())
                return BadRequest("Cannot delete this position because it still has employees.");

            _context.Positions.Remove(position);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}