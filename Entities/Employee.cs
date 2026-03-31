namespace smart_hr_attendance_payroll_management.Entities
{
    public class Employee
    {
        public int Id { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal BaseSalary { get; set; }
        public DateTime HireDate { get; set; }
        public bool IsActive { get; set; } = true;

        public int DepartmentId { get; set; }
        public Department? Department { get; set; }

        public int PositionId { get; set; }
        public Position? Position { get; set; }
    }
}