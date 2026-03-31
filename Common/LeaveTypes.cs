using System;
using System.Linq;

namespace smart_hr_attendance_payroll_management.Common
{
    public static class LeaveTypes
    {
        public const string AnnualLeave = "AnnualLeave";
        public const string SickLeave = "SickLeave";
        public const string UnpaidLeave = "UnpaidLeave";
        public const string Other = "Other";

        public static readonly string[] All =
        {
            AnnualLeave,
            SickLeave,
            UnpaidLeave,
            Other
        };

        public static bool IsValid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return All.Any(x => x.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static string Normalize(string value)
        {
            var normalized = value.Trim();

            var matched = All.FirstOrDefault(x =>
                x.Equals(normalized, StringComparison.OrdinalIgnoreCase));

            return matched ?? normalized;
        }
    }
}