namespace smart_hr_attendance_payroll_management.DTOs
{
    public class AiPayrollSummaryResponse
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int? DepartmentId { get; set; }
        public string Summary { get; set; } = string.Empty;
        public IReadOnlyList<string> Highlights { get; set; } = Array.Empty<string>();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public bool FromCache { get; set; }
        public string TemplateKey { get; set; } = "anomaly-overview";
        public string TemplateTitle { get; set; } = "Anomaly Overview";
        public string PromptUsed { get; set; } = string.Empty;
    }
}
