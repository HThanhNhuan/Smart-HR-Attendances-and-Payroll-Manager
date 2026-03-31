using System.ComponentModel.DataAnnotations;

namespace smart_hr_attendance_payroll_management.DTOs
{
    public class CreateLeaveRequestRequest : IValidatableObject
    {
        [Required]
        [MaxLength(30)]
        public string LeaveType { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        [Required]
        [MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartDate == default)
            {
                yield return new ValidationResult(
                    "StartDate is required.",
                    new[] { nameof(StartDate) });
            }

            if (EndDate == default)
            {
                yield return new ValidationResult(
                    "EndDate is required.",
                    new[] { nameof(EndDate) });
            }

            if (StartDate != default && EndDate != default && EndDate.Date < StartDate.Date)
            {
                yield return new ValidationResult(
                    "EndDate cannot be earlier than StartDate.",
                    new[] { nameof(StartDate), nameof(EndDate) });
            }
        }
    }
}