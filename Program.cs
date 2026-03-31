using Microsoft.EntityFrameworkCore;
using smart_hr_attendance_payroll_management.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using smart_hr_attendance_payroll_management.Models;
using smart_hr_attendance_payroll_management.Services;
using System.Text;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;
using smart_hr_attendance_payroll_management.Middleware;
using Microsoft.Extensions.Caching.StackExchangeRedis;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddJsonConsole();

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "SmartHr.AttendancePayroll.Api", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter token like: Bearer {your JWT token}"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<RefreshTokenService>();
builder.Services.AddScoped<DistributedCacheService>();
builder.Services.AddScoped<PayrollComputationService>();
builder.Services.AddScoped<LeaveBalancePolicyService>();
builder.Services.AddScoped<PayrollSummaryAssistantService>();
builder.Services.AddHttpContextAccessor();

var redisConnection = builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();
QuestPDF.Settings.License = LicenseType.Community;

if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();

        await context.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[dbo].[AttendanceAdjustmentRequests]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AttendanceAdjustmentRequests](
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [EmployeeId] INT NOT NULL,
        [AttendanceId] INT NULL,
        [WorkDate] DATE NOT NULL,
        [RequestedCheckInTime] DATETIME2 NULL,
        [RequestedCheckOutTime] DATETIME2 NULL,
        [RequestedStatus] NVARCHAR(20) NOT NULL,
        [Reason] NVARCHAR(1000) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [ReviewedByUserId] INT NULL,
        [ReviewedAt] DATETIME2 NULL,
        [ReviewNote] NVARCHAR(1000) NULL,
        CONSTRAINT [FK_AttendanceAdjustmentRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employees]([Id]),
        CONSTRAINT [FK_AttendanceAdjustmentRequests_Attendances_AttendanceId] FOREIGN KEY ([AttendanceId]) REFERENCES [dbo].[Attendances]([Id]),
        CONSTRAINT [FK_AttendanceAdjustmentRequests_AppUsers_ReviewedByUserId] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [dbo].[AppUsers]([Id]),
        CONSTRAINT [CK_AttendanceAdjustmentRequests_RequestedStatus] CHECK ([RequestedStatus] IN ('Present', 'Late', 'Absent', 'Leave', 'Remote')),
        CONSTRAINT [CK_AttendanceAdjustmentRequests_Status] CHECK ([Status] IN ('Pending', 'Approved', 'Rejected')),
        CONSTRAINT [CK_AttendanceAdjustmentRequests_CheckOutTime] CHECK ([RequestedCheckOutTime] IS NULL OR [RequestedCheckInTime] IS NULL OR [RequestedCheckOutTime] >= [RequestedCheckInTime])
    );
    CREATE INDEX [IX_AttendanceAdjustmentRequests_EmployeeId] ON [dbo].[AttendanceAdjustmentRequests]([EmployeeId]);
    CREATE INDEX [IX_AttendanceAdjustmentRequests_Status] ON [dbo].[AttendanceAdjustmentRequests]([Status]);
    CREATE INDEX [IX_AttendanceAdjustmentRequests_EmployeeId_WorkDate_Status] ON [dbo].[AttendanceAdjustmentRequests]([EmployeeId], [WorkDate], [Status]);
END");

        await context.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[dbo].[LeaveRequestAuditLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[LeaveRequestAuditLogs](
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [LeaveRequestId] INT NOT NULL,
        [PerformedByUserId] INT NULL,
        [ActionType] NVARCHAR(50) NOT NULL,
        [PreviousStatus] NVARCHAR(20) NULL,
        [NewStatus] NVARCHAR(20) NULL,
        [Note] NVARCHAR(2000) NULL,
        [SnapshotJson] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [FK_LeaveRequestAuditLogs_LeaveRequests_LeaveRequestId] FOREIGN KEY ([LeaveRequestId]) REFERENCES [dbo].[LeaveRequests]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_LeaveRequestAuditLogs_AppUsers_PerformedByUserId] FOREIGN KEY ([PerformedByUserId]) REFERENCES [dbo].[AppUsers]([Id])
    );
    CREATE INDEX [IX_LeaveRequestAuditLogs_LeaveRequestId] ON [dbo].[LeaveRequestAuditLogs]([LeaveRequestId]);
    CREATE INDEX [IX_LeaveRequestAuditLogs_CreatedAt] ON [dbo].[LeaveRequestAuditLogs]([CreatedAt]);
END");

        await context.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[dbo].[PayrollAuditLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PayrollAuditLogs](
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [PayrollId] INT NOT NULL,
        [EmployeeId] INT NOT NULL,
        [EmployeeCode] NVARCHAR(50) NOT NULL,
        [EmployeeFullName] NVARCHAR(200) NOT NULL,
        [PayrollMonth] INT NOT NULL,
        [PayrollYear] INT NOT NULL,
        [BaseSalary] DECIMAL(18,2) NOT NULL,
        [Bonus] DECIMAL(18,2) NOT NULL,
        [Deduction] DECIMAL(18,2) NOT NULL,
        [NetSalary] DECIMAL(18,2) NOT NULL,
        [PerformedByUserId] INT NULL,
        [ActionType] NVARCHAR(50) NOT NULL,
        [Note] NVARCHAR(2000) NULL,
        [SnapshotJson] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [FK_PayrollAuditLogs_AppUsers_PerformedByUserId] FOREIGN KEY ([PerformedByUserId]) REFERENCES [dbo].[AppUsers]([Id])
    );
    CREATE INDEX [IX_PayrollAuditLogs_PayrollId] ON [dbo].[PayrollAuditLogs]([PayrollId]);
    CREATE INDEX [IX_PayrollAuditLogs_CreatedAt] ON [dbo].[PayrollAuditLogs]([CreatedAt]);
    CREATE INDEX [IX_PayrollAuditLogs_EmployeeId_PayrollMonth_PayrollYear] ON [dbo].[PayrollAuditLogs]([EmployeeId], [PayrollMonth], [PayrollYear]);
END");

        await context.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[dbo].[AttendanceAdjustmentAuditLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AttendanceAdjustmentAuditLogs](
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [AttendanceAdjustmentRequestId] INT NOT NULL,
        [EmployeeId] INT NOT NULL,
        [EmployeeCode] NVARCHAR(50) NOT NULL,
        [EmployeeFullName] NVARCHAR(200) NOT NULL,
        [WorkDate] DATE NOT NULL,
        [RequestedStatus] NVARCHAR(20) NOT NULL,
        [CurrentStatus] NVARCHAR(20) NOT NULL,
        [PerformedByUserId] INT NULL,
        [ActionType] NVARCHAR(50) NOT NULL,
        [PreviousStatus] NVARCHAR(20) NULL,
        [NewStatus] NVARCHAR(20) NULL,
        [Note] NVARCHAR(2000) NULL,
        [SnapshotJson] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [FK_AttendanceAdjustmentAuditLogs_AttendanceAdjustmentRequests_AttendanceAdjustmentRequestId] FOREIGN KEY ([AttendanceAdjustmentRequestId]) REFERENCES [dbo].[AttendanceAdjustmentRequests]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AttendanceAdjustmentAuditLogs_AppUsers_PerformedByUserId] FOREIGN KEY ([PerformedByUserId]) REFERENCES [dbo].[AppUsers]([Id])
    );
    CREATE INDEX [IX_AttendanceAdjustmentAuditLogs_AttendanceAdjustmentRequestId] ON [dbo].[AttendanceAdjustmentAuditLogs]([AttendanceAdjustmentRequestId]);
    CREATE INDEX [IX_AttendanceAdjustmentAuditLogs_CreatedAt] ON [dbo].[AttendanceAdjustmentAuditLogs]([CreatedAt]);
    CREATE INDEX [IX_AttendanceAdjustmentAuditLogs_EmployeeId_WorkDate] ON [dbo].[AttendanceAdjustmentAuditLogs]([EmployeeId], [WorkDate]);
END");

        await context.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Payrolls', 'PaidLeaveDays') IS NULL ALTER TABLE [dbo].[Payrolls] ADD [PaidLeaveDays] DECIMAL(10,2) NOT NULL CONSTRAINT [DF_Payrolls_PaidLeaveDays] DEFAULT(0);
IF COL_LENGTH('dbo.Payrolls', 'UnpaidLeaveDays') IS NULL ALTER TABLE [dbo].[Payrolls] ADD [UnpaidLeaveDays] DECIMAL(10,2) NOT NULL CONSTRAINT [DF_Payrolls_UnpaidLeaveDays] DEFAULT(0);
IF COL_LENGTH('dbo.Payrolls', 'OvertimeHours') IS NULL ALTER TABLE [dbo].[Payrolls] ADD [OvertimeHours] DECIMAL(10,2) NOT NULL CONSTRAINT [DF_Payrolls_OvertimeHours] DEFAULT(0);
IF COL_LENGTH('dbo.Payrolls', 'ApprovedOvertimeRequests') IS NULL ALTER TABLE [dbo].[Payrolls] ADD [ApprovedOvertimeRequests] INT NOT NULL CONSTRAINT [DF_Payrolls_ApprovedOvertimeRequests] DEFAULT(0);
IF COL_LENGTH('dbo.Payrolls', 'OvertimePay') IS NULL ALTER TABLE [dbo].[Payrolls] ADD [OvertimePay] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_Payrolls_OvertimePay] DEFAULT(0);

IF OBJECT_ID(N'[dbo].[WorkShifts]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorkShifts](
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ShiftCode] NVARCHAR(50) NOT NULL,
        [ShiftName] NVARCHAR(100) NOT NULL,
        [StartTime] TIME NOT NULL,
        [EndTime] TIME NOT NULL,
        [StandardHours] DECIMAL(10,2) NOT NULL,
        [IsNightShift] BIT NOT NULL DEFAULT(0),
        [IsActive] BIT NOT NULL DEFAULT(1),
        [Notes] NVARCHAR(1000) NULL,
        [CreatedAt] DATETIME2 NOT NULL
    );
    CREATE UNIQUE INDEX [IX_WorkShifts_ShiftCode] ON [dbo].[WorkShifts]([ShiftCode]);
END

IF OBJECT_ID(N'[dbo].[WorkSchedules]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorkSchedules](
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [EmployeeId] INT NOT NULL,
        [WorkShiftId] INT NOT NULL,
        [WorkDate] DATE NOT NULL,
        [Notes] NVARCHAR(1000) NULL,
        [IsLocked] BIT NOT NULL DEFAULT(0),
        [CreatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [FK_WorkSchedules_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employees]([Id]),
        CONSTRAINT [FK_WorkSchedules_WorkShifts_WorkShiftId] FOREIGN KEY ([WorkShiftId]) REFERENCES [dbo].[WorkShifts]([Id])
    );
    CREATE UNIQUE INDEX [IX_WorkSchedules_EmployeeId_WorkDate] ON [dbo].[WorkSchedules]([EmployeeId],[WorkDate]);
END

IF OBJECT_ID(N'[dbo].[OvertimeRequests]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OvertimeRequests](
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [EmployeeId] INT NOT NULL,
        [WorkDate] DATE NOT NULL,
        [StartTime] DATETIME2 NOT NULL,
        [EndTime] DATETIME2 NOT NULL,
        [Hours] DECIMAL(10,2) NOT NULL,
        [Reason] NVARCHAR(1000) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL,
        [ApprovedByUserId] INT NULL,
        [ApprovedAt] DATETIME2 NULL,
        [ApprovalNote] NVARCHAR(1000) NULL,
        [RejectionReason] NVARCHAR(1000) NULL,
        [AppliedToPayroll] BIT NOT NULL DEFAULT(0),
        [CreatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [FK_OvertimeRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employees]([Id]),
        CONSTRAINT [FK_OvertimeRequests_AppUsers_ApprovedByUserId] FOREIGN KEY ([ApprovedByUserId]) REFERENCES [dbo].[AppUsers]([Id]),
        CONSTRAINT [CK_OvertimeRequests_Status] CHECK ([Status] IN ('Pending','Approved','Rejected','Cancelled')),
        CONSTRAINT [CK_OvertimeRequests_Hours] CHECK ([Hours] > 0)
    );
END

IF OBJECT_ID(N'[dbo].[LeaveBalances]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[LeaveBalances](
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [EmployeeId] INT NOT NULL,
        [Year] INT NOT NULL,
        [AnnualAllocated] DECIMAL(10,2) NOT NULL DEFAULT(12),
        [AnnualUsed] DECIMAL(10,2) NOT NULL DEFAULT(0),
        [SickAllocated] DECIMAL(10,2) NOT NULL DEFAULT(6),
        [SickUsed] DECIMAL(10,2) NOT NULL DEFAULT(0),
        [CarryForward] DECIMAL(10,2) NOT NULL DEFAULT(0),
        [UnpaidDays] DECIMAL(10,2) NOT NULL DEFAULT(0),
        [UpdatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [FK_LeaveBalances_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employees]([Id])
    );
    CREATE UNIQUE INDEX [IX_LeaveBalances_EmployeeId_Year] ON [dbo].[LeaveBalances]([EmployeeId],[Year]);
END

IF OBJECT_ID(N'[dbo].[PayrollPeriods]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PayrollPeriods](
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [PayrollMonth] INT NOT NULL,
        [PayrollYear] INT NOT NULL,
        [IsLocked] BIT NOT NULL DEFAULT(0),
        [Note] NVARCHAR(1000) NULL,
        [LockedByUserId] INT NULL,
        [LockedAt] DATETIME2 NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [FK_PayrollPeriods_AppUsers_LockedByUserId] FOREIGN KEY ([LockedByUserId]) REFERENCES [dbo].[AppUsers]([Id])
    );
    CREATE UNIQUE INDEX [IX_PayrollPeriods_PayrollYear_PayrollMonth] ON [dbo].[PayrollPeriods]([PayrollYear],[PayrollMonth]);
END

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_AppUsers_Role')
BEGIN
    ALTER TABLE [dbo].[AppUsers] DROP CONSTRAINT [CK_AppUsers_Role];
END
ALTER TABLE [dbo].[AppUsers] WITH NOCHECK ADD CONSTRAINT [CK_AppUsers_Role] CHECK ([Role] IN ('Admin','HR','Employee','Manager'));
");

        await context.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[dbo].[RefreshTokens]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RefreshTokens](
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [UserId] INT NOT NULL,
        [Token] NVARCHAR(512) NOT NULL,
        [ExpiresAt] DATETIME2 NOT NULL,
        [RevokedAt] DATETIME2 NULL,
        [ReplacedByToken] NVARCHAR(512) NULL,
        [CreatedByIp] NVARCHAR(100) NULL,
        [RevokedByIp] NVARCHAR(100) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [FK_RefreshTokens_AppUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AppUsers]([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_RefreshTokens_Token] ON [dbo].[RefreshTokens]([Token]);
END

IF OBJECT_ID(N'[dbo].[AuditLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AuditLogs](
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [UserId] INT NULL,
        [Action] NVARCHAR(20) NOT NULL,
        [EntityName] NVARCHAR(100) NOT NULL,
        [EntityId] NVARCHAR(100) NULL,
        [OldValue] NVARCHAR(MAX) NULL,
        [NewValue] NVARCHAR(MAX) NULL,
        [HttpMethod] NVARCHAR(10) NOT NULL,
        [Path] NVARCHAR(500) NOT NULL,
        [StatusCode] INT NOT NULL,
        [TraceId] NVARCHAR(200) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [FK_AuditLogs_AppUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AppUsers]([Id])
    );
    CREATE INDEX [IX_AuditLogs_CreatedAt] ON [dbo].[AuditLogs]([CreatedAt]);
END
");
    } // <-- This closing brace was missing
} // <-- This closing brace was missing

await DbSeeder.SeedAdminAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<RequestAuditMiddleware>();

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();


public partial class Program { }
