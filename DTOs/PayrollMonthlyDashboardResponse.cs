namespace smart_hr_attendance_payroll_management.DTOs
{
    public class PayrollMonthlyDashboardResponse
    {
        public int Year { get; set; }
        public int Month { get; set; }

        public int TotalPayrollRecords { get; set; }

        public decimal TotalBaseSalary { get; set; }
        public decimal TotalBonus { get; set; }
        public decimal TotalDeduction { get; set; }
        public decimal TotalNetSalary { get; set; }

        public decimal AverageNetSalary { get; set; }
    }
}