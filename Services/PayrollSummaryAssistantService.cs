using Microsoft.EntityFrameworkCore;
using smart_hr_attendance_payroll_management.Common;
using smart_hr_attendance_payroll_management.Data;
using smart_hr_attendance_payroll_management.DTOs;

namespace smart_hr_attendance_payroll_management.Services
{
    public class PayrollSummaryAssistantService
    {
        private readonly AppDbContext _context;

        private static readonly IReadOnlyList<AiPromptTemplateResponse> Templates = new List<AiPromptTemplateResponse>
        {
            new()
            {
                Key = "anomaly-overview",
                Title = "Anomaly Overview",
                Description = "Best all-round summary for payroll anomalies, deductions, overtime pressure, and attendance risk.",
                PromptTemplate = "Summarize payroll anomalies for {month}/{year}. Focus on unusual deductions, overtime impact, attendance issues, and net-salary outliers.",
                ExamplePrompt = "Focus on unusual deductions, overtime impact, and payroll outliers for the selected month."
            },
            new()
            {
                Key = "executive-brief",
                Title = "Executive Brief",
                Description = "Short leadership-style brief for HR/Admin reports and demo narration.",
                PromptTemplate = "Write a concise executive payroll brief for {month}/{year} covering workforce pressure, pending issues, and headline payroll signals.",
                ExamplePrompt = "Write a short executive summary for HR leadership and keep it presentation-ready."
            },
            new()
            {
                Key = "department-focus",
                Title = "Department Focus",
                Description = "Use when you want a department-centered explanation of payroll, overtime, and attendance signals.",
                PromptTemplate = "Analyze department-level payroll signals for {month}/{year}. Emphasize attendance pressure, overtime concentration, and notable net-salary patterns.",
                ExamplePrompt = "Highlight which department appears under the most attendance or overtime pressure."
            },
            new()
            {
                Key = "overtime-watch",
                Title = "Overtime Watch",
                Description = "Highlights overtime-heavy payroll patterns and operational load.",
                PromptTemplate = "Explain unusual overtime contribution to payroll for {month}/{year}, including which employees or departments drive the change.",
                ExamplePrompt = "Explain which employees or teams caused overtime-heavy payroll this month."
            }
        };

        public PayrollSummaryAssistantService(AppDbContext context)
        {
            _context = context;
        }

        public IReadOnlyList<AiPromptTemplateResponse> GetTemplates() => Templates;

        public async Task<AiPayrollSummaryResponse> SummarizeAsync(AiPayrollSummaryRequest request, int? managerDepartmentId = null)
        {
            var payrollQuery = _context.Payrolls.AsNoTracking().Include(x => x.Employee).ThenInclude(e => e.Department).Where(x => x.PayrollYear == request.Year && x.PayrollMonth == request.Month);
            var attendanceQuery = _context.Attendances.AsNoTracking().Include(x => x.Employee).ThenInclude(e => e.Department).Where(x => x.WorkDate.Year == request.Year && x.WorkDate.Month == request.Month);
            var overtimeQuery = _context.OvertimeRequests.AsNoTracking().Include(x => x.Employee).ThenInclude(e => e.Department).Where(x => x.WorkDate.Year == request.Year && x.WorkDate.Month == request.Month && x.Status == OvertimeStatuses.Approved);

            var departmentId = request.DepartmentId ?? managerDepartmentId;
            if (departmentId.HasValue)
            {
                payrollQuery = payrollQuery.Where(x => x.Employee != null && x.Employee.DepartmentId == departmentId.Value);
                attendanceQuery = attendanceQuery.Where(x => x.Employee != null && x.Employee.DepartmentId == departmentId.Value);
                overtimeQuery = overtimeQuery.Where(x => x.Employee != null && x.Employee.DepartmentId == departmentId.Value);
            }

            var payrolls = await payrollQuery.ToListAsync();
            var attendances = await attendanceQuery.ToListAsync();
            var overtime = await overtimeQuery.ToListAsync();

            var template = Templates.FirstOrDefault(t => string.Equals(t.Key, request.TemplateKey, StringComparison.OrdinalIgnoreCase))
                ?? Templates[0];

            var promptUsed = BuildPrompt(template, request, departmentId);
            var highlights = new List<string>();

            if (!payrolls.Any())
            {
                highlights.Add("No payroll records were generated for the selected period.");
            }
            else
            {
                var avgNet = payrolls.Average(x => x.NetSalary);
                var highest = payrolls.OrderByDescending(x => x.NetSalary).First();
                var lowest = payrolls.OrderBy(x => x.NetSalary).First();
                var deductionHeavy = payrolls.Where(x => x.Deduction > 0).OrderByDescending(x => x.Deduction).Take(3).ToList();
                var overtimeHeavy = payrolls.Where(x => x.OvertimePay > 0).OrderByDescending(x => x.OvertimePay).Take(3).ToList();
                var lateEmployees = attendances.Where(x => x.Status == AttendanceStatuses.Late).GroupBy(x => x.EmployeeId).OrderByDescending(g => g.Count()).Take(3).ToList();
                var departmentSignals = payrolls.Where(x => x.Employee?.Department != null).GroupBy(x => x.Employee!.Department!.DepartmentName).Select(g => new { Department = g.Key, Net = g.Sum(x => x.NetSalary), Count = g.Count() }).OrderByDescending(x => x.Net).Take(3).ToList();

                highlights.Add($"{payrolls.Count} payroll record(s) were analyzed with an average net salary of {avgNet:0,0} VND.");
                highlights.Add($"Highest net salary: {highest.Employee?.FullName ?? $"Employee #{highest.EmployeeId}"} at {highest.NetSalary:0,0} VND.");
                highlights.Add($"Lowest net salary: {lowest.Employee?.FullName ?? $"Employee #{lowest.EmployeeId}"} at {lowest.NetSalary:0,0} VND.");
                if (deductionHeavy.Any())
                    highlights.Add("Largest deductions: " + string.Join(", ", deductionHeavy.Select(x => $"{x.Employee?.FullName ?? x.EmployeeId.ToString()} ({x.Deduction:0,0})")) + ".");
                if (overtimeHeavy.Any())
                    highlights.Add("Most overtime-driven payrolls: " + string.Join(", ", overtimeHeavy.Select(x => $"{x.Employee?.FullName ?? x.EmployeeId.ToString()} ({x.OvertimePay:0,0})")) + ".");
                if (lateEmployees.Any())
                    highlights.Add("Top late-attendance patterns: " + string.Join(", ", lateEmployees.Select(g => $"Employee #{g.Key} ({g.Count()} late record(s))")) + ".");
                if (overtime.Any())
                    highlights.Add($"Approved overtime requests in the period: {overtime.Count}, totalling {overtime.Sum(x => x.Hours):0.##} hour(s).");
                if (departmentSignals.Any())
                    highlights.Add("Departments with the strongest payroll footprint: " + string.Join(", ", departmentSignals.Select(x => $"{x.Department} ({x.Count} payrolls / {x.Net:0,0} VND)")) + ".");
            }

            var narrative = BuildNarrative(template.Key, request, payrolls, attendances, overtime, departmentId, highlights);

            return new AiPayrollSummaryResponse
            {
                Year = request.Year,
                Month = request.Month,
                DepartmentId = departmentId,
                Summary = narrative,
                Highlights = highlights,
                TemplateKey = template.Key,
                TemplateTitle = template.Title,
                PromptUsed = promptUsed
            };
        }

        private static string BuildPrompt(AiPromptTemplateResponse template, AiPayrollSummaryRequest request, int? departmentId)
        {
            var monthLabel = $"{request.Month:00}/{request.Year}";
            var basePrompt = template.PromptTemplate.Replace("{month}", $"{request.Month:00}").Replace("{year}", request.Year.ToString());
            var deptPart = departmentId.HasValue ? $" Department focus: #{departmentId.Value}." : " Department focus: all departments.";
            var custom = string.IsNullOrWhiteSpace(request.Prompt) ? string.Empty : $" Custom instruction: {request.Prompt.Trim()}";
            return $"Template [{template.Title}] for {monthLabel}.{deptPart} {basePrompt}{custom}".Trim();
        }

        private static string BuildNarrative(string templateKey, AiPayrollSummaryRequest request, IReadOnlyCollection<Entities.Payroll> payrolls, IReadOnlyCollection<Entities.Attendance> attendances, IReadOnlyCollection<Entities.OvertimeRequest> overtime, int? departmentId, IReadOnlyList<string> highlights)
        {
            var monthLabel = $"{request.Month:00}/{request.Year}";
            var deptLabel = departmentId.HasValue ? $"department #{departmentId.Value}" : "the full organization";
            var lateCount = attendances.Count(x => x.Status == AttendanceStatuses.Late);
            var absentCount = attendances.Count(x => x.Status == AttendanceStatuses.Absent);
            var totalNet = payrolls.Sum(x => x.NetSalary);
            var totalOvertimePay = payrolls.Sum(x => x.OvertimePay);

            return templateKey switch
            {
                "executive-brief" => $"Executive brief for {monthLabel}: {deptLabel} produced {payrolls.Count} payroll record(s) with total net salary of {totalNet:0,0} VND. Attendance pressure included {lateCount} late record(s) and {absentCount} absence record(s), while approved overtime contributed {totalOvertimePay:0,0} VND to payroll. Key signals: {string.Join(" ", highlights.Take(3))}",
                "department-focus" => $"Department-focused analysis for {monthLabel}: {deptLabel} shows {payrolls.Count} payroll row(s), {attendances.Count} attendance record(s), and {overtime.Count} approved overtime request(s). This suggests a workload profile shaped by {lateCount} late record(s), {absentCount} absence record(s), and overtime pay of {totalOvertimePay:0,0} VND. Key signals: {string.Join(" ", highlights.Take(4))}",
                "overtime-watch" => $"Overtime watch for {monthLabel}: approved overtime requests total {overtime.Count} and feed {totalOvertimePay:0,0} VND into payroll for {deptLabel}. Attendance shows {lateCount} late record(s), which may indicate workload pressure around overtime-heavy employees. Key signals: {string.Join(" ", highlights.Where(h => h.Contains("overtime", StringComparison.OrdinalIgnoreCase) || h.Contains("late", StringComparison.OrdinalIgnoreCase)).DefaultIfEmpty(highlights.FirstOrDefault() ?? string.Empty))}",
                _ => $"Payroll anomaly overview for {monthLabel}: {deptLabel} produced {payrolls.Count} payroll record(s) with total net salary of {totalNet:0,0} VND. Attendance contributed {lateCount} late record(s) and {absentCount} absence record(s), while approved overtime added {totalOvertimePay:0,0} VND. Key signals: {string.Join(" ", highlights)}"
            };
        }
    }
}
