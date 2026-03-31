namespace smart_hr_attendance_payroll_management.Entities
{
    public class OvertimeRequest
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }
        public DateTime WorkDate { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public decimal Hours { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? ApprovedByUserId { get; set; }
        public AppUser? ApprovedByUser { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? ApprovalNote { get; set; }
        public string? RejectionReason { get; set; }
        public bool AppliedToPayroll { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
