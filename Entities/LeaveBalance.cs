namespace smart_hr_attendance_payroll_management.Entities
{
    public class LeaveBalance
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }
        public int Year { get; set; }
        public decimal AnnualAllocated { get; set; } = 12;
        public decimal AnnualUsed { get; set; }
        public decimal SickAllocated { get; set; } = 6;
        public decimal SickUsed { get; set; }
        public decimal CarryForward { get; set; }
        public decimal UnpaidDays { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
