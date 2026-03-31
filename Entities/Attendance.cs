using smart_hr_attendance_payroll_management.Common;

namespace smart_hr_attendance_payroll_management.Entities
{
    public class Attendance
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        public DateTime WorkDate { get; set; }
        public DateTime CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }

        public string Status { get; set; } = AttendanceStatuses.Present;
        public string? Note { get; set; }

        public string? SourceType { get; set; }
        public int? SourceReferenceId { get; set; }
    }
}