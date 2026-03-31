namespace smart_hr_attendance_payroll_management.DTOs
{
    public class RecentLeaveRequestResponse
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        public string LeaveType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalDays { get; set; }

        public string Reason { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? ApprovedByUsername { get; set; }
    }
}