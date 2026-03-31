using System.ComponentModel.DataAnnotations;

namespace smart_hr_attendance_payroll_management.DTOs
{
    public class ReviewAttendanceAdjustmentRequest
    {
        [MaxLength(1000)]
        public string? ReviewNote { get; set; }
    }
}
