namespace smart_hr_attendance_payroll_management.DTOs
{
    public class UpdateWorkScheduleRequest : CreateWorkScheduleRequest
    {
        public bool IsLocked { get; set; }
    }
}
