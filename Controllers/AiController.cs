using Microsoft.EntityFrameworkCore;
using smart_hr_attendance_payroll_management.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_hr_attendance_payroll_management.Common;
using smart_hr_attendance_payroll_management.DTOs;
using smart_hr_attendance_payroll_management.Services;

namespace smart_hr_attendance_payroll_management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,HR,Manager")]
    public class AiController : ControllerBase
    {
        private readonly PayrollSummaryAssistantService _assistant;
        private readonly DistributedCacheService _cache;
        private readonly AppDbContext _context;

        public AiController(PayrollSummaryAssistantService assistant, DistributedCacheService cache, AppDbContext context)
        {
            _assistant = assistant;
            _cache = cache;
            _context = context;
        }

        [HttpGet("payroll-summary/templates")]
        public IActionResult PayrollSummaryTemplates()
        {
            return Ok(_assistant.GetTemplates());
        }

        [HttpPost("payroll-summary")]
        public async Task<IActionResult> PayrollSummary([FromBody] AiPayrollSummaryRequest request)
        {
            if (request.Month < 1 || request.Month > 12) return BadRequest("Month must be between 1 and 12.");
            if (request.Year < 2000 || request.Year > 2100) return BadRequest("Year is invalid.");

            var managerDepartmentId = await GetManagedDepartmentIdAsync();
            if (managerDepartmentId.HasValue)
                request.DepartmentId ??= managerDepartmentId;

            var normalizedPrompt = request.Prompt?.Trim().ToLowerInvariant() ?? string.Empty;
            var normalizedTemplate = request.TemplateKey?.Trim().ToLowerInvariant() ?? "anomaly-overview";
            var cacheKey = $"ai:payroll-summary:{request.Year}:{request.Month}:{request.DepartmentId}:{normalizedTemplate}:{normalizedPrompt}";
            var cached = await _cache.GetAsync<AiPayrollSummaryResponse>(cacheKey);
            if (cached != null)
            {
                cached.FromCache = true;
                return Ok(cached);
            }

            var response = await _assistant.SummarizeAsync(request, managerDepartmentId);
            await _cache.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5));
            return Ok(response);
        }

        private async Task<int?> GetManagedDepartmentIdAsync()
        {
            if (!User.IsInRole(UserRoles.Manager)) return null;
            var employeeIdClaim = User.FindFirst("employeeId")?.Value;
            if (!int.TryParse(employeeIdClaim, out var employeeId)) return null;
            return await _context.Employees.AsNoTracking().Where(e => e.Id == employeeId).Select(e => (int?)e.DepartmentId).FirstOrDefaultAsync();
        }
    }
}
