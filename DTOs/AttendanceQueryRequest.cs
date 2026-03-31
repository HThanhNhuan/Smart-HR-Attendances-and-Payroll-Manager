using System.ComponentModel.DataAnnotations;
using smart_hr_attendance_payroll_management.Validation;

namespace smart_hr_attendance_payroll_management.DTOs
{
    public class AttendanceQueryRequest : IValidatableObject
    {
        [Range(1, int.MaxValue)]
        public int? EmployeeId { get; set; }

        public DateTime? WorkDate { get; set; }

        [Range(1, 12)]
        public int? Month { get; set; }

        [Range(2000, 2100)]
        public int? Year { get; set; }

        [AttendanceStatus]
        public string? Status { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Month.HasValue && !Year.HasValue)
            {
                yield return new ValidationResult(
                    "Year is required when Month is provided.",
                    new[] { nameof(Year) });
            }

            if (WorkDate.HasValue && (Month.HasValue || Year.HasValue))
            {
                yield return new ValidationResult(
                    "Use either WorkDate or Month/Year filter, not both.",
                    new[] { nameof(WorkDate), nameof(Month), nameof(Year) });
            }
        }
    }
}