namespace smart_hr_attendance_payroll_management.DTOs
{
    public class AttendanceAdjustmentRequestResponse
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int PositionId { get; set; }
        public string PositionName { get; set; } = string.Empty;
        public int? AttendanceId { get; set; }
        public DateTime WorkDate { get; set; }
        public DateTime? RequestedCheckInTime { get; set; }
        public DateTime? RequestedCheckOutTime { get; set; }
        public string RequestedStatus { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int? ReviewedByUserId { get; set; }
        public string? ReviewedByUsername { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNote { get; set; }
    }
}
