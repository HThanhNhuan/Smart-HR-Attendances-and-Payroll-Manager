namespace smart_hr_attendance_payroll_management.Entities
{
    public class Payroll
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        public int PayrollMonth { get; set; }
        public int PayrollYear { get; set; }

        public decimal BaseSalary { get; set; }
        public decimal DailySalary { get; set; }

        public int PresentDays { get; set; }
        public int LateDays { get; set; }
        public int RemoteDays { get; set; }
        public int AbsentDays { get; set; }
        public int LeaveDays { get; set; }

        public int EffectiveWorkingDays { get; set; }

        public decimal PaidLeaveDays { get; set; }
        public decimal UnpaidLeaveDays { get; set; }
        public decimal OvertimeHours { get; set; }
        public int ApprovedOvertimeRequests { get; set; }

        public decimal Bonus { get; set; }
        public decimal Deduction { get; set; }
        public decimal OvertimePay { get; set; }
        public decimal NetSalary { get; set; }

        public DateTime GeneratedAt { get; set; }
    }
}