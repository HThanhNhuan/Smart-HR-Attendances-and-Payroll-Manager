namespace smart_hr_attendance_payroll_management.DTOs
{
    public class EmployeeStatusSummaryResponse
    {
        public int TotalEmployees { get; set; }
        public int ActiveEmployees { get; set; }
        public int InactiveEmployees { get; set; }

        public decimal ActivePercentage { get; set; }
        public decimal InactivePercentage { get; set; }
    }
}