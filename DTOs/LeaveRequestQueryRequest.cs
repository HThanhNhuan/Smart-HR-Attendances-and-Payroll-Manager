using System.ComponentModel.DataAnnotations;

namespace smart_hr_attendance_payroll_management.DTOs
{
    public class LeaveRequestQueryRequest : IValidatableObject
    {
        [Range(1, int.MaxValue)]
        public int? EmployeeId { get; set; }

        [MaxLength(20)]
        public string? Status { get; set; }

        [MaxLength(30)]
        public string? LeaveType { get; set; }

        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (FromDate.HasValue && ToDate.HasValue && ToDate.Value.Date < FromDate.Value.Date)
            {
                yield return new ValidationResult(
                    "ToDate cannot be earlier than FromDate.",
                    new[] { nameof(FromDate), nameof(ToDate) });
            }
        }
    }
}