namespace smart_hr_attendance_payroll_management.DTOs
{
    public class EmployeeProfileResponse
    {
        public int Id { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal BaseSalary { get; set; }
        public DateTime HireDate { get; set; }
        public bool IsActive { get; set; }

        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;

        public int PositionId { get; set; }
        public string PositionName { get; set; } = string.Empty;
    }
}