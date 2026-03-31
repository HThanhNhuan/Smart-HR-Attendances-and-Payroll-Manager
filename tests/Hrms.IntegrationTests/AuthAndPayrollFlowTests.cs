using System.Net.Http.Json;
using System.Net.Http.Headers;
using smart_hr_attendance_payroll_management.DTOs;

namespace Hrms.IntegrationTests;

public class AuthAndPayrollFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthAndPayrollFlowTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_ReturnsAccessAndRefreshTokens()
    {
        var response = await _client.PostAsJsonAsync("/api/Auth/login", new LoginRequest { Username = "emp001", Password = "emp123" });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(payload?.Token));
        Assert.False(string.IsNullOrWhiteSpace(payload?.RefreshToken));
    }

    [Fact]
    public async Task Refresh_ReturnsNewTokens()
    {
        var login = await _client.PostAsJsonAsync("/api/Auth/login", new LoginRequest { Username = "emp001", Password = "emp123" });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        var refresh = await _client.PostAsJsonAsync("/api/Auth/refresh", new RefreshTokenRequest { RefreshToken = auth!.RefreshToken });
        refresh.EnsureSuccessStatusCode();
        var payload = await refresh.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotEqual(auth.RefreshToken, payload!.RefreshToken);
    }

    [Fact]
    public async Task AuthenticatedEmployee_CanReadLeaveBalance_AndPayrolls()
    {
        var login = await _client.PostAsJsonAsync("/api/Auth/login", new LoginRequest { Username = "emp001", Password = "emp123" });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var balanceResponse = await _client.GetAsync("/api/LeaveRequests/my-balance?year=2026");
        balanceResponse.EnsureSuccessStatusCode();

        var payrollResponse = await _client.GetAsync("/api/Payrolls/my-payrolls?month=3&year=2026");
        payrollResponse.EnsureSuccessStatusCode();
    }
}
