namespace smart_hr_attendance_payroll_management.DTOs
{
    public class GenerateAllPayrollsResponse
    {
        public int Month { get; set; }
        public int Year { get; set; }

        public int TotalEmployees { get; set; }
        public int CreatedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedCount { get; set; }

        public List<PayrollResponse> Payrolls { get; set; } = new();
    }
}