namespace smart_hr_attendance_payroll_management.DTOs
{
    public class DashboardOverviewResponse
    {
        public int Year { get; set; }
        public int Month { get; set; }

        public int TotalEmployees { get; set; }
        public int ActiveEmployees { get; set; }
        public int InactiveEmployees { get; set; }

        public int TotalDepartments { get; set; }
        public int TotalPositions { get; set; }

        public int PendingLeaveRequests { get; set; }

        public int MonthlyAttendanceRecords { get; set; }
        public int MonthlyPayrollRecords { get; set; }

        public int MonthlyPresentCount { get; set; }
        public int MonthlyLateCount { get; set; }
        public int MonthlyAbsentCount { get; set; }
        public int MonthlyLeaveCount { get; set; }
        public int MonthlyRemoteCount { get; set; }

        public decimal MonthlyTotalNetSalary { get; set; }
    }
}