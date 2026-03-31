using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using smart_hr_attendance_payroll_management.Data;
using smart_hr_attendance_payroll_management.Entities;
using smart_hr_attendance_payroll_management.Services;

namespace Hrms.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor != null) services.Remove(dbDescriptor);

            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("SmartHrIntegrationTests"));

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
            Seed(db);
        });
    }

    private static void Seed(AppDbContext db)
    {
        var passwordService = new PasswordService();
        passwordService.CreatePasswordHash("emp123", out var hash, out var salt);
        var dept = new smart_hr_attendance_payroll_management.Department.Department { Id = 1, DepartmentCode = "DEP01", DepartmentName = "Human Resources" };
        var position = new Position { Id = 1, PositionCode = "POS01", PositionName = "HR Executive" };
        var employee = new Employee { Id = 1, EmployeeCode = "EMP001", FullName = "Test Employee", Email = "emp001@test.local", DepartmentId = 1, PositionId = 1, HireDate = new DateTime(2025, 1, 10), BaseSalary = 12000000m, IsActive = true };
        var user = new AppUser { Id = 1, Username = "emp001", PasswordHash = hash, PasswordSalt = salt, Role = "Employee", EmployeeId = 1, IsActive = true };
        db.Departments.Add(dept);
        db.Positions.Add(position);
        db.Employees.Add(employee);
        db.AppUsers.Add(user);
        db.Payrolls.Add(new Payroll { Id = 1, EmployeeId = 1, PayrollMonth = 3, PayrollYear = 2026, BaseSalary = 12000000m, DailySalary = 500000m, PresentDays = 20, LateDays = 0, RemoteDays = 0, AbsentDays = 0, LeaveDays = 0, EffectiveWorkingDays = 20m, PaidLeaveDays = 0m, UnpaidLeaveDays = 0m, OvertimeHours = 2m, ApprovedOvertimeRequests = 1, OvertimePay = 187500m, Bonus = 200000m, Deduction = 100000m, NetSalary = 10287500m, GeneratedAt = DateTime.UtcNow });
        db.LeaveBalances.Add(new LeaveBalance { Id = 1, EmployeeId = 1, Year = 2026, AnnualAllocated = 12m, AnnualUsed = 2m, SickAllocated = 6m, SickUsed = 1m, CarryForward = 3m, UnpaidDays = 0m, UpdatedAt = DateTime.UtcNow });
        db.SaveChanges();
    }
}
