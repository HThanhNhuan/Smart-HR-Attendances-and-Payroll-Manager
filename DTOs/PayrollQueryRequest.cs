using System.ComponentModel.DataAnnotations;

namespace smart_hr_attendance_payroll_management.DTOs
{
    public class PayrollQueryRequest
    {
        [Range(1, int.MaxValue)]
        public int? EmployeeId { get; set; }

        [Range(1, 12)]
        public int? Month { get; set; }

        [Range(2000, 2100)]
        public int? Year { get; set; }
    }
}