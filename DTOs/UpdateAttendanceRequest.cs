using System.ComponentModel.DataAnnotations;
using smart_hr_attendance_payroll_management.Common;
using smart_hr_attendance_payroll_management.Validation;

namespace smart_hr_attendance_payroll_management.DTOs
{
    public class UpdateAttendanceRequest : IValidatableObject
    {
        [Range(1, int.MaxValue)]
        public int EmployeeId { get; set; }

        public DateTime WorkDate { get; set; }

        public DateTime CheckInTime { get; set; }

        public DateTime? CheckOutTime { get; set; }

        [Required]
        [AttendanceStatus]
        [MaxLength(20)]
        public string Status { get; set; } = AttendanceStatuses.Present;

        [MaxLength(500)]
        public string? Note { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (WorkDate == default)
            {
                yield return new ValidationResult(
                    "WorkDate is required.",
                    new[] { nameof(WorkDate) });
            }

            if (CheckInTime == default)
            {
                yield return new ValidationResult(
                    "CheckInTime is required.",
                    new[] { nameof(CheckInTime) });
            }

            var workDate = WorkDate.Date;

            if (CheckInTime != default && CheckInTime.Date != workDate)
            {
                yield return new ValidationResult(
                    "CheckInTime must be on the same date as WorkDate.",
                    new[] { nameof(CheckInTime), nameof(WorkDate) });
            }

            if (CheckOutTime.HasValue)
            {
                if (CheckOutTime.Value.Date != workDate)
                {
                    yield return new ValidationResult(
                        "CheckOutTime must be on the same date as WorkDate.",
                        new[] { nameof(CheckOutTime), nameof(WorkDate) });
                }

                if (CheckOutTime.Value < CheckInTime)
                {
                    yield return new ValidationResult(
                        "CheckOutTime cannot be earlier than CheckInTime.",
                        new[] { nameof(CheckOutTime), nameof(CheckInTime) });
                }
            }
        }
    }
}