using smart_hr_attendance_payroll_management.Services;

namespace Hrms.UnitTests;

public class LeaveBalancePolicyServiceTests
{
    private readonly LeaveBalancePolicyService _service = new();

    [Fact]
    public void CalculateAnnualAllocation_ProrationForHireYear()
    {
        var result = _service.CalculateAnnualAllocation(new DateTime(2026, 7, 10), 2026);
        Assert.Equal(6m, result);
    }

    [Fact]
    public void CalculateCarryForward_CapsAtFiveDays()
    {
        var result = _service.CalculateCarryForward(12m, 0m, 3m, 5m);
        Assert.Equal(5m, result);
    }

    [Fact]
    public void GetAnnualAvailableAfterPending_SubtractsUsedAndPending()
    {
        var result = _service.GetAnnualAvailableAfterPending(12m, 2m, 4m, 3m);
        Assert.Equal(7m, result);
    }
}
