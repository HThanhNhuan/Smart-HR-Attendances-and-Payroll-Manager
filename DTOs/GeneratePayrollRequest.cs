using System.ComponentModel.DataAnnotations;

namespace smart_hr_attendance_payroll_management.DTOs
{
    public class GeneratePayrollRequest
    {
        [Range(1, int.MaxValue)]
        public int EmployeeId { get; set; }

        [Range(1, 12)]
        public int Month { get; set; }

        [Range(2000, 2100)]
        public int Year { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Bonus { get; set; } = 0;

        [Range(0, double.MaxValue)]
        public decimal Deduction { get; set; } = 0;
    }
}