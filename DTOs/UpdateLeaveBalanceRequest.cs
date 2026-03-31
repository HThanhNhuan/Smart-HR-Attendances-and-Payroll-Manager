namespace smart_hr_attendance_payroll_management.DTOs
{
    public class UpdateLeaveBalanceRequest
    {
        public decimal AnnualAllocated { get; set; }
        public decimal AnnualUsed { get; set; }
        public decimal SickAllocated { get; set; }
        public decimal SickUsed { get; set; }
        public decimal CarryForward { get; set; }
        public decimal UnpaidDays { get; set; }
    }
}
