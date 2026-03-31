namespace smart_hr_attendance_payroll_management.DTOs
{
    public class RecentPayrollResponse
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        public int PayrollMonth { get; set; }
        public int PayrollYear { get; set; }

        public decimal BaseSalary { get; set; }
        public decimal Bonus { get; set; }
        public decimal Deduction { get; set; }
        public decimal NetSalary { get; set; }

        public DateTime GeneratedAt { get; set; }
    }
}