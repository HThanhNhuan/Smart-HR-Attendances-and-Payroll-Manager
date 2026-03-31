using System;
using System.Linq;

namespace smart_hr_attendance_payroll_management.Common
{
    public static class AttendanceStatuses
    {
        public const string Present = "Present";
        public const string Late = "Late";
        public const string Absent = "Absent";
        public const string Leave = "Leave";
        public const string Remote = "Remote";

        public static readonly string[] All =
        {
            Present,
            Late,
            Absent,
            Leave,
            Remote
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