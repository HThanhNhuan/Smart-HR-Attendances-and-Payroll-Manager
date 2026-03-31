using System.ComponentModel.DataAnnotations;

namespace smart_hr_attendance_payroll_management.DTOs
{
    public class UpdateDepartmentRequest
    {
        [Required]
        [MaxLength(50)]
        public string DepartmentCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string DepartmentName { get; set; } = string.Empty;
    }
}