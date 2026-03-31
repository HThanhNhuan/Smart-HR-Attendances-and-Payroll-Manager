namespace smart_hr_attendance_payroll_management.DTOs
{
    public class LeaveMonthlyDashboardResponse
    {
        public int Year { get; set; }
        public int Month { get; set; }

        public int TotalRequests { get; set; }
        public int PendingRequests { get; set; }
        public int ApprovedRequests { get; set; }
        public int RejectedRequests { get; set; }
        public int CancelledRequests { get; set; }

        public int ApprovedLeaveDays { get; set; }

        public int AnnualLeaveRequests { get; set; }
        public int SickLeaveRequests { get; set; }
        public int UnpaidLeaveRequests { get; set; }
        public int OtherLeaveRequests { get; set; }
    }
}