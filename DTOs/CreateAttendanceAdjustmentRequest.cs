using System.ComponentModel.DataAnnotations;
using smart_hr_attendance_payroll_management.Common;
using smart_hr_attendance_payroll_management.Validation;

namespace smart_hr_attendance_payroll_management.DTOs
{
    public class CreateAttendanceAdjustmentRequest : IValidatableObject
    {
        public DateTime WorkDate { get; set; }

        public DateTime? RequestedCheckInTime { get; set; }
        public DateTime? RequestedCheckOutTime { get; set; }

        [Required]
        [AttendanceStatus]
        [MaxLength(20)]
        public string RequestedStatus { get; set; } = AttendanceStatuses.Present;

        [Required]
        [MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (WorkDate == default)
            {
                yield return new ValidationResult(
                    "WorkDate is required.",
                    new[] { nameof(WorkDate) });
            }

            if (string.IsNullOrWhiteSpace(Reason))
            {
                yield return new ValidationResult(
                    "Reason is required.",
                    new[] { nameof(Reason) });
            }

            var normalizedStatus = AttendanceStatuses.Normalize(RequestedStatus);
            var workDate = WorkDate.Date;
            var needsWorkTime = normalizedStatus == AttendanceStatuses.Present ||
                                normalizedStatus == AttendanceStatuses.Late ||
                                normalizedStatus == AttendanceStatuses.Remote;

            if (needsWorkTime && !RequestedCheckInTime.HasValue)
            {
                yield return new ValidationResult(
                    "RequestedCheckInTime is required for working attendance statuses.",
                    new[] { nameof(RequestedCheckInTime), nameof(RequestedStatus) });
            }

            if (RequestedCheckInTime.HasValue && RequestedCheckInTime.Value.Date != workDate)
            {
                yield return new ValidationResult(
                    "RequestedCheckInTime must be on the same date as WorkDate.",
                    new[] { nameof(RequestedCheckInTime), nameof(WorkDate) });
            }

            if (RequestedCheckOutTime.HasValue)
            {
                if (RequestedCheckOutTime.Value.Date != workDate)
                {
                    yield return new ValidationResult(
                        "RequestedCheckOutTime must be on the same date as WorkDate.",
                        new[] { nameof(RequestedCheckOutTime), nameof(WorkDate) });
                }

                if (RequestedCheckInTime.HasValue && RequestedCheckOutTime.Value < RequestedCheckInTime.Value)
                {
                    yield return new ValidationResult(
                        "RequestedCheckOutTime cannot be earlier than RequestedCheckInTime.",
                        new[] { nameof(RequestedCheckOutTime), nameof(RequestedCheckInTime) });
                }
            }
        }
    }
}
