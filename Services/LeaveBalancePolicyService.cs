namespace smart_hr_attendance_payroll_management.Services
{
    public class LeaveBalancePolicyService
    {
        public decimal CalculateAnnualAllocation(DateTime hireDate, int year)
        {
            if (hireDate.Year < year) return 12m;
            if (hireDate.Year > year) return 0m;
            var workedMonths = 12 - hireDate.Month + 1;
            return Math.Round((12m / 12m) * workedMonths, 2);
        }

        public decimal CalculateCarryForward(decimal annualAllocated, decimal carryForward, decimal annualUsed, decimal cap = 5m)
        {
            var remaining = (annualAllocated + carryForward) - annualUsed;
            return Math.Max(Math.Min(remaining, cap), 0m);
        }

        public decimal GetAnnualAvailableAfterPending(decimal annualAllocated, decimal carryForward, decimal annualUsed, decimal pendingAnnual)
            => (annualAllocated + carryForward) - annualUsed - pendingAnnual;

        public decimal GetSickAvailableAfterPending(decimal sickAllocated, decimal sickUsed, decimal pendingSick)
            => sickAllocated - sickUsed - pendingSick;
    }
}
