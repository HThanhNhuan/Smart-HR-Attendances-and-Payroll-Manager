namespace smart_hr_attendance_payroll_management.DTOs
{
    public class UpdateWorkShiftRequest : CreateWorkShiftRequest
    {
        public bool IsActive { get; set; } = true;
    }
}
