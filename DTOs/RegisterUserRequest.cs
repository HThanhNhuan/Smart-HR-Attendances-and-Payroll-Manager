using System.ComponentModel.DataAnnotations;

namespace smart_hr_attendance_payroll_management.DTOs
{
    public class RegisterUserRequest
    {
        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        [MaxLength(100)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Role { get; set; } = string.Empty;

        public int? EmployeeId { get; set; }
    }
}