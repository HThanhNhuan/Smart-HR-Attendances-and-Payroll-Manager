namespace smart_hr_attendance_payroll_management.DTOs
{
    public class AttendanceMonthlyDashboardResponse
    {
        public int Year { get; set; }
        public int Month { get; set; }

        public int TotalAttendanceRecords { get; set; }

        public int PresentCount { get; set; }
        public int LateCount { get; set; }
        public int AbsentCount { get; set; }
        public int LeaveCount { get; set; }
        public int RemoteCount { get; set; }
    }
}