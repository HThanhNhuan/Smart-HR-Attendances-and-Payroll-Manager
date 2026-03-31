namespace smart_hr_attendance_payroll_management.Entities
{
    public class PayrollPeriod
    {
        public int Id { get; set; }
        public int PayrollMonth { get; set; }
        public int PayrollYear { get; set; }
        public bool IsLocked { get; set; }
        public string? Note { get; set; }
        public int? LockedByUserId { get; set; }
        public AppUser? LockedByUser { get; set; }
        public DateTime? LockedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}