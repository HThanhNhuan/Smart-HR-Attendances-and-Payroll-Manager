using System.ComponentModel.DataAnnotations;

namespace smart_hr_attendance_payroll_management.DTOs
{
    public class ChangePasswordRequest : IValidatableObject
    {
        [Required]
        [MaxLength(100)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        [MaxLength(100)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        [MaxLength(100)]
        public string ConfirmNewPassword { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (NewPassword != ConfirmNewPassword)
            {
                yield return new ValidationResult(
                    "ConfirmNewPassword does not match NewPassword.",
                    new[] { nameof(ConfirmNewPassword), nameof(NewPassword) });
            }

            if (CurrentPassword == NewPassword)
            {
                yield return new ValidationResult(
                    "NewPassword must be different from CurrentPassword.",
                    new[] { nameof(NewPassword), nameof(CurrentPassword) });
            }
        }
    }
}