namespace smart_hr_attendance_payroll_management.Entities
{
    public class AttendanceAdjustmentAuditLog
    {
        public int Id { get; set; }
        public int AttendanceAdjustmentRequestId { get; set; }
        public AttendanceAdjustmentRequest? AttendanceAdjustmentRequest { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string EmployeeFullName { get; set; } = string.Empty;
        public DateTime WorkDate { get; set; }
        public string RequestedStatus { get; set; } = string.Empty;
        public string CurrentStatus { get; set; } = string.Empty;
        public int? PerformedByUserId { get; set; }
        public AppUser? PerformedByUser { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string? PreviousStatus { get; set; }
        public string? NewStatus { get; set; }
        public string? Note { get; set; }
        public string? SnapshotJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
