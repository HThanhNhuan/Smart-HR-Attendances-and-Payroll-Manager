using System.ComponentModel.DataAnnotations;

namespace smart_hr_attendance_payroll_management.DTOs
{
    public class ApproveLeaveRequest
    {
        [MaxLength(1000)]
        public string? ApprovalNote { get; set; }
    }
}