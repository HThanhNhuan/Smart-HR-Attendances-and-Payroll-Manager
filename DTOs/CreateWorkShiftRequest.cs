namespace smart_hr_attendance_payroll_management.DTOs
{
    public class CreateWorkShiftRequest
    {
        public string ShiftCode { get; set; } = string.Empty;
        public string ShiftName { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public decimal StandardHours { get; set; }
        public bool IsNightShift { get; set; }
        public string? Notes { get; set; }
    }
}
