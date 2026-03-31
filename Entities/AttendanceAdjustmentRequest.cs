using smart_hr_attendance_payroll_management.Common;

namespace smart_hr_attendance_payroll_management.Entities
{
    public class AttendanceAdjustmentRequest
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        public int? AttendanceId { get; set; }
        public Attendance? Attendance { get; set; }

        public DateTime WorkDate { get; set; }
        public DateTime? RequestedCheckInTime { get; set; }
        public DateTime? RequestedCheckOutTime { get; set; }

        public string RequestedStatus { get; set; } = AttendanceStatuses.Present;
        public string Reason { get; set; } = string.Empty;

        public string Status { get; set; } = AttendanceAdjustmentStatuses.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? ReviewedByUserId { get; set; }
        public AppUser? ReviewedByUser { get; set; }

        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNote { get; set; }
    }
}
