namespace smart_hr_attendance_payroll_management.DTOs
{
    public class RecentAttendanceResponse
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        public string DepartmentName { get; set; } = string.Empty;
        public string PositionName { get; set; } = string.Empty;

        public DateTime WorkDate { get; set; }
        public DateTime CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }

        public string Status { get; set; } = string.Empty;
        public string? Note { get; set; }

        public string? SourceType { get; set; }
        public int? SourceReferenceId { get; set; }
    }
}