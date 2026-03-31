namespace smart_hr_attendance_payroll_management.Entities
{
    public class WorkSchedule
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }
        public int WorkShiftId { get; set; }
        public WorkShift? WorkShift { get; set; }
        public DateTime WorkDate { get; set; }
        public string? Notes { get; set; }
        public bool IsLocked { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
