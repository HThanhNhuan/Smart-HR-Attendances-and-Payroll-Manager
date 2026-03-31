namespace smart_hr_attendance_payroll_management.DTOs
{
    public class AiPayrollSummaryRequest
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int? DepartmentId { get; set; }
        public string? Prompt { get; set; }
        public string? TemplateKey { get; set; }
    }
}
