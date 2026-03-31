namespace smart_hr_attendance_payroll_management.Common
{
    public static class OvertimeStatuses
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";

        public static readonly string[] All = new[] { Pending, Approved, Rejected, Cancelled };

        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Pending;
            return All.FirstOrDefault(x => x.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Pending;
        }
    }
}
