namespace smart_hr_attendance_payroll_management.Services
{
    public class PayrollComputationService
    {
        public decimal ComputeDailySalary(decimal baseSalary, int workingDaysInMonth)
        {
            if (workingDaysInMonth <= 0) return 0m;
            return Math.Round(baseSalary / workingDaysInMonth, 2);
        }

        public decimal ComputeOvertimePay(decimal dailySalary, decimal overtimeHours, decimal multiplier)
        {
            if (dailySalary <= 0 || overtimeHours <= 0 || multiplier <= 0) return 0m;
            var hourlyRate = dailySalary / 8m;
            return Math.Round(hourlyRate * overtimeHours * multiplier, 2);
        }

        public decimal ComputeNetSalary(decimal dailySalary, decimal actualWorkingDays, decimal paidLeaveDays, decimal overtimePay, decimal bonus, decimal deduction)
        {
            return Math.Round(((actualWorkingDays + paidLeaveDays) * dailySalary) + overtimePay + bonus - deduction, 2);
        }
    }
}
