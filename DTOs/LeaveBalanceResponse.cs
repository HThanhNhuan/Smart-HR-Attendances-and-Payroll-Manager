namespace smart_hr_attendance_payroll_management.DTOs
{
    public class LeaveBalanceResponse
    {
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int Year { get; set; }
        public decimal AnnualAllocated { get; set; }
        public decimal AnnualUsed { get; set; }
        public decimal AnnualRemaining { get; set; }
        public decimal SickAllocated { get; set; }
        public decimal SickUsed { get; set; }
        public decimal SickRemaining { get; set; }
        public decimal CarryForward { get; set; }
        public decimal UnpaidDays { get; set; }
        public DateTime UpdatedAt { get; set; }

        public decimal AnnualPending { get; set; }
        public decimal SickPending { get; set; }
        public decimal AnnualAvailableAfterPending { get; set; }
        public decimal SickAvailableAfterPending { get; set; }
    }
}
