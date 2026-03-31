namespace smart_hr_attendance_payroll_management.DTOs
{
    public class CreateOvertimeRequest
    {
        public DateTime WorkDate { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
