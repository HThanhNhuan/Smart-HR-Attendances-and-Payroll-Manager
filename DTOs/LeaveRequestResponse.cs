namespace smart_hr_attendance_payroll_management.DTOs
{
    public class LeaveRequestResponse
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;

        public int PositionId { get; set; }
        public string PositionName { get; set; } = string.Empty;

        public string LeaveType { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalDays { get; set; }

        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        public int? ApprovedByUserId { get; set; }
        public string? ApprovedByUsername { get; set; }

        public DateTime? ApprovedAt { get; set; }
        public string? ApprovalNote { get; set; }
        public string? RejectionReason { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}