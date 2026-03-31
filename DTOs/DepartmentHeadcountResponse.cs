namespace smart_hr_attendance_payroll_management.DTOs
{
    public class DepartmentHeadcountResponse
    {
        public int DepartmentId { get; set; }
        public string DepartmentCode { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public int EmployeeCount { get; set; }
        public int ActiveEmployeeCount { get; set; }
        public int InactiveEmployeeCount { get; set; }
    }
}