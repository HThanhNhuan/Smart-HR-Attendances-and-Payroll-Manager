using System;
using System.Linq;

namespace smart_hr_attendance_payroll_management.Common
{
    public static class LeaveStatuses
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";

        public static readonly string[] All =
        {
            Pending,
            Approved,
            Rejected,
            Cancelled
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