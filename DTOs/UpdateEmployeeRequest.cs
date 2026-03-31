using System.ComponentModel.DataAnnotations;

namespace smart_hr_attendance_payroll_management.DTOs
{
    public class UpdateEmployeeRequest
    {
        [Required]
        [MaxLength(50)]
        public string EmployeeCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Range(0.01, double.MaxValue)]
        public decimal BaseSalary { get; set; }

        public DateTime HireDate { get; set; }

        public bool IsActive { get; set; } = true;

        [Range(1, int.MaxValue)]
        public int DepartmentId { get; set; }

        [Range(1, int.MaxValue)]
        public int PositionId { get; set; }

        public bool HasLoginAccount { get; set; }

        [MaxLength(50)]
        public string? Username { get; set; }

        [MaxLength(100)]
        public string? NewPassword { get; set; }

        [MaxLength(20)]
        public string? AccountRole { get; set; }
    }
}
