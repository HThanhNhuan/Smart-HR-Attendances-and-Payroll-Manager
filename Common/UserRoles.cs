namespace smart_hr_attendance_payroll_management.Common
{
    public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string HR = "HR";
        public const string Employee = "Employee";
        public const string Manager = "Manager";

        public static readonly string[] All = new[]
        {
            Admin,
            HR,
            Employee,
            Manager
        };
    }
}