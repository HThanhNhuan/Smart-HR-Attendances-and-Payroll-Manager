using smart_hr_attendance_payroll_management.Services;

namespace Hrms.UnitTests;

public class PayrollComputationServiceTests
{
    private readonly PayrollComputationService _service = new();

    [Fact]
    public void ComputeDailySalary_ReturnsRoundedAmount()
    {
        var result = _service.ComputeDailySalary(26000000m, 26);
        Assert.Equal(1000000m, result);
    }

    [Fact]
    public void ComputeOvertimePay_UsesHourlyRateAndMultiplier()
    {
        var result = _service.ComputeOvertimePay(1000000m, 2m, 1.5m);
        Assert.Equal(375000m, result);
    }

    [Fact]
    public void ComputeNetSalary_AddsPaidLeaveAndOvertimeMinusDeduction()
    {
        var result = _service.ComputeNetSalary(1000000m, 20m, 2m, 500000m, 300000m, 100000m);
        Assert.Equal(22700000m, result);
    }
}
