using System.ComponentModel.DataAnnotations;

namespace smart_hr_attendance_payroll_management.DTOs
{
    public class CreatePositionRequest
    {
        [Required]
        [MaxLength(50)]
        public string PositionCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string PositionName { get; set; } = string.Empty;
    }
}