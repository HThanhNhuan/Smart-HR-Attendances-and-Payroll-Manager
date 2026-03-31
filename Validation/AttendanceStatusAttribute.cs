using System.ComponentModel.DataAnnotations;
using smart_hr_attendance_payroll_management.Common;

namespace smart_hr_attendance_payroll_management.Validation
{
    public class AttendanceStatusAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value is null)
                return true;

            if (value is string status)
                return AttendanceStatuses.IsValid(status);

            return false;
        }

        public override string FormatErrorMessage(string name)
        {
            return $"{name} must be one of: {string.Join(", ", AttendanceStatuses.All)}.";
        }
    }
}