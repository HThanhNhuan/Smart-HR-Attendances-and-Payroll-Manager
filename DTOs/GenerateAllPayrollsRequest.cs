using System.ComponentModel.DataAnnotations;

namespace smart_hr_attendance_payroll_management.DTOs
{
    public class GenerateAllPayrollsRequest
    {
        [Range(1, 12)]
        public int Month { get; set; }

        [Range(2000, 2100)]
        public int Year { get; set; }

        public bool OverwriteExisting { get; set; } = false;
    }
}