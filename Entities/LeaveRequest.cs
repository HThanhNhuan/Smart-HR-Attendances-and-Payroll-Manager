using smart_hr_attendance_payroll_management.Common;
namespace smart_hr_attendance_payroll_management.Entities
{
    public class LeaveRequest
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        public string LeaveType { get; set; } = LeaveTypes.AnnualLeave;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public int TotalDays { get; set; }

        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = LeaveStatuses.Pending;

        public int? ApprovedByUserId { get; set; }
        public AppUser? ApprovedByUser { get; set; }

        public DateTime? ApprovedAt { get; set; }
        public string? ApprovalNote { get; set; }
        public string? RejectionReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}