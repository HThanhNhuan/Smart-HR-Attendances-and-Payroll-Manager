namespace smart_hr_attendance_payroll_management.Entities
{
    public class Position
    {
        public int Id { get; set; }
        public string PositionCode { get; set; } = string.Empty;
        public string PositionName { get; set; } = string.Empty;

        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}