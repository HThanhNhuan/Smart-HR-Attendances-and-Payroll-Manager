namespace smart_hr_attendance_payroll_management.Entities
{
    public class LeaveRequestAuditLog
    {
        public int Id { get; set; }
        public int LeaveRequestId { get; set; }
        public LeaveRequest? LeaveRequest { get; set; }
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
