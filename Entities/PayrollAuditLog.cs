namespace smart_hr_attendance_payroll_management.Entities
{
    public class PayrollAuditLog
    {
        public int Id { get; set; }
        public int PayrollId { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string EmployeeFullName { get; set; } = string.Empty;
        public int PayrollMonth { get; set; }
        public int PayrollYear { get; set; }
        public decimal BaseSalary { get; set; }
        public decimal Bonus { get; set; }
        public decimal Deduction { get; set; }
        public decimal NetSalary { get; set; }
        public int? PerformedByUserId { get; set; }
        public AppUser? PerformedByUser { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string? SnapshotJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
