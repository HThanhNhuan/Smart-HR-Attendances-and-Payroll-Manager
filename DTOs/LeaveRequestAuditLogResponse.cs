namespace smart_hr_attendance_payroll_management.DTOs
{
    public class LeaveRequestAuditLogResponse
    {
        public int Id { get; set; }
        public int LeaveRequestId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string? PreviousStatus { get; set; }
        public string? NewStatus { get; set; }
        public string? Note { get; set; }
        public string? SnapshotJson { get; set; }
        public int? PerformedByUserId { get; set; }
        public string? PerformedByUsername { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
