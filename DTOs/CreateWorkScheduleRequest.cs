namespace smart_hr_attendance_payroll_management.DTOs
{
    public class CreateWorkScheduleRequest
    {
        public int EmployeeId { get; set; }
        public int WorkShiftId { get; set; }
        public DateTime WorkDate { get; set; }
        public string? Notes { get; set; }
    }
}
