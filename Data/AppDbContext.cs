using Microsoft.EntityFrameworkCore;
using smart_hr_attendance_payroll_management.Entities;

namespace smart_hr_attendance_payroll_management.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Employee> Employees { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Position> Positions { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<Payroll> Payrolls { get; set; }
        public DbSet<AppUser> AppUsers { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<AttendanceAdjustmentRequest> AttendanceAdjustmentRequests { get; set; }
        public DbSet<LeaveRequestAuditLog> LeaveRequestAuditLogs { get; set; }
        public DbSet<PayrollAuditLog> PayrollAuditLogs { get; set; }
        public DbSet<AttendanceAdjustmentAuditLog> AttendanceAdjustmentAuditLogs { get; set; }
        public DbSet<WorkShift> WorkShifts { get; set; }
        public DbSet<WorkSchedule> WorkSchedules { get; set; }
        public DbSet<OvertimeRequest> OvertimeRequests { get; set; }
        public DbSet<LeaveBalance> LeaveBalances { get; set; }
        public DbSet<PayrollPeriod> PayrollPeriods { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Employee>(entity =>
            {
                entity.Property(e => e.EmployeeCode).HasMaxLength(50);
                entity.Property(e => e.FullName).HasMaxLength(200);
                entity.Property(e => e.Email).HasMaxLength(100);
                entity.Property(e => e.BaseSalary).HasPrecision(18, 2);
                entity.HasIndex(e => e.EmployeeCode).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasOne(e => e.Department).WithMany(d => d.Employees).HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Position).WithMany(p => p.Employees).HasForeignKey(e => e.PositionId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Department>(entity =>
            {
                entity.Property(d => d.DepartmentCode).HasMaxLength(50);
                entity.Property(d => d.DepartmentName).HasMaxLength(100);
                entity.HasIndex(d => d.DepartmentCode).IsUnique();
            });

            modelBuilder.Entity<Position>(entity =>
            {
                entity.Property(p => p.PositionCode).HasMaxLength(50);
                entity.Property(p => p.PositionName).HasMaxLength(100);
                entity.HasIndex(p => p.PositionCode).IsUnique();
            });


            modelBuilder.Entity<PayrollPeriod>(entity =>
            {
                entity.Property(x => x.Note).HasMaxLength(1000);
                entity.HasIndex(x => new { x.PayrollYear, x.PayrollMonth }).IsUnique();
                entity.HasOne(x => x.LockedByUser).WithMany().HasForeignKey(x => x.LockedByUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Attendance>(entity =>
            {
                entity.Property(a => a.WorkDate).HasColumnType("date");
                entity.Property(a => a.Status).HasMaxLength(20).IsRequired();
                entity.Property(a => a.Note).HasMaxLength(500);
                entity.Property(a => a.SourceType).HasMaxLength(30);
                entity.HasIndex(a => new { a.EmployeeId, a.WorkDate }).IsUnique();
                entity.HasIndex(a => a.WorkDate);
                entity.HasIndex(a => new { a.WorkDate, a.Status });
                entity.HasIndex(a => new { a.SourceType, a.SourceReferenceId });
                entity.HasOne(a => a.Employee).WithMany().HasForeignKey(a => a.EmployeeId).OnDelete(DeleteBehavior.Restrict);
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Attendances_Status", "[Status] IN ('Present', 'Late', 'Absent', 'Leave', 'Remote')");
                    t.HasCheckConstraint("CK_Attendances_CheckOutTime", "[CheckOutTime] IS NULL OR [CheckOutTime] >= [CheckInTime]");
                    t.HasCheckConstraint("CK_Attendances_SourceType", "[SourceType] IS NULL OR [SourceType] IN ('Manual', 'ApprovedLeave')");
                });
            });

            modelBuilder.Entity<Payroll>(entity =>
            {
                entity.Property(p => p.BaseSalary).HasPrecision(18, 2);
                entity.Property(p => p.DailySalary).HasPrecision(18, 2);
                entity.Property(p => p.Bonus).HasPrecision(18, 2);
                entity.Property(p => p.Deduction).HasPrecision(18, 2);
                entity.Property(p => p.NetSalary).HasPrecision(18, 2);
                entity.Property(p => p.PaidLeaveDays).HasPrecision(10, 2);
                entity.Property(p => p.UnpaidLeaveDays).HasPrecision(10, 2);
                entity.Property(p => p.OvertimeHours).HasPrecision(10, 2);
                entity.Property(p => p.OvertimePay).HasPrecision(18, 2);
                entity.HasIndex(p => new { p.EmployeeId, p.PayrollMonth, p.PayrollYear }).IsUnique();
                entity.HasOne(p => p.Employee).WithMany().HasForeignKey(p => p.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AppUser>(entity =>
            {
                entity.Property(u => u.Username).HasMaxLength(50).IsRequired();
                entity.Property(u => u.Role).HasMaxLength(20).IsRequired();
                entity.HasIndex(u => u.Username).IsUnique();
                entity.HasIndex(u => u.EmployeeId).IsUnique().HasFilter("[EmployeeId] IS NOT NULL");
                entity.HasOne(u => u.Employee).WithMany().HasForeignKey(u => u.EmployeeId).OnDelete(DeleteBehavior.Restrict);
                entity.ToTable(t => t.HasCheckConstraint("CK_AppUsers_Role", "[Role] IN ('Admin', 'HR', 'Employee', 'Manager')"));
            });

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.Property(x => x.Token).HasMaxLength(512).IsRequired();
                entity.Property(x => x.CreatedByIp).HasMaxLength(100);
                entity.Property(x => x.RevokedByIp).HasMaxLength(100);
                entity.Property(x => x.ReplacedByToken).HasMaxLength(512);
                entity.HasIndex(x => x.Token).IsUnique();
                entity.HasIndex(x => new { x.UserId, x.ExpiresAt });
                entity.HasOne(x => x.User).WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.Property(x => x.Action).HasMaxLength(20).IsRequired();
                entity.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
                entity.Property(x => x.EntityId).HasMaxLength(100);
                entity.Property(x => x.HttpMethod).HasMaxLength(10).IsRequired();
                entity.Property(x => x.Path).HasMaxLength(500).IsRequired();
                entity.Property(x => x.TraceId).HasMaxLength(200);
                entity.Property(x => x.OldValue).HasColumnType("nvarchar(max)");
                entity.Property(x => x.NewValue).HasColumnType("nvarchar(max)");
                entity.HasIndex(x => x.CreatedAt);
                entity.HasIndex(x => new { x.UserId, x.CreatedAt });
                entity.HasOne(x => x.User).WithMany(u => u.AuditLogs).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AttendanceAdjustmentRequest>(entity =>
            {
                entity.Property(a => a.WorkDate).HasColumnType("date");
                entity.Property(a => a.RequestedStatus).HasMaxLength(20).IsRequired();
                entity.Property(a => a.Reason).HasMaxLength(1000).IsRequired();
                entity.Property(a => a.Status).HasMaxLength(20).IsRequired();
                entity.Property(a => a.ReviewNote).HasMaxLength(1000);
                entity.HasIndex(a => a.EmployeeId);
                entity.HasIndex(a => a.Status);
                entity.HasIndex(a => new { a.EmployeeId, a.WorkDate, a.Status });
                entity.HasOne(a => a.Employee).WithMany().HasForeignKey(a => a.EmployeeId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(a => a.Attendance).WithMany().HasForeignKey(a => a.AttendanceId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(a => a.ReviewedByUser).WithMany().HasForeignKey(a => a.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_AttendanceAdjustmentRequests_RequestedStatus", "[RequestedStatus] IN ('Present', 'Late', 'Absent', 'Leave', 'Remote')");
                    t.HasCheckConstraint("CK_AttendanceAdjustmentRequests_Status", "[Status] IN ('Pending', 'Approved', 'Rejected')");
                    t.HasCheckConstraint("CK_AttendanceAdjustmentRequests_CheckOutTime", "[RequestedCheckOutTime] IS NULL OR [RequestedCheckInTime] IS NULL OR [RequestedCheckOutTime] >= [RequestedCheckInTime]");
                });
            });

            modelBuilder.Entity<LeaveRequest>(entity =>
            {
                entity.Property(l => l.LeaveType).HasMaxLength(30).IsRequired();
                entity.Property(l => l.StartDate).HasColumnType("date");
                entity.Property(l => l.EndDate).HasColumnType("date");
                entity.Property(l => l.Reason).HasMaxLength(1000).IsRequired();
                entity.Property(l => l.Status).HasMaxLength(20).IsRequired();
                entity.Property(l => l.ApprovalNote).HasMaxLength(1000);
                entity.Property(l => l.RejectionReason).HasMaxLength(1000);
                entity.HasIndex(l => l.EmployeeId);
                entity.HasIndex(l => l.Status);
                entity.HasIndex(l => new { l.EmployeeId, l.Status });
                entity.HasIndex(l => new { l.EmployeeId, l.StartDate, l.EndDate });
                entity.HasOne(l => l.Employee).WithMany().HasForeignKey(l => l.EmployeeId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(l => l.ApprovedByUser).WithMany().HasForeignKey(l => l.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_LeaveRequests_Status", "[Status] IN ('Pending', 'Approved', 'Rejected', 'Cancelled')");
                    t.HasCheckConstraint("CK_LeaveRequests_LeaveType", "[LeaveType] IN ('AnnualLeave', 'SickLeave', 'UnpaidLeave', 'Other')");
                    t.HasCheckConstraint("CK_LeaveRequests_DateRange", "[EndDate] >= [StartDate]");
                    t.HasCheckConstraint("CK_LeaveRequests_TotalDays", "[TotalDays] > 0");
                });
            });

            modelBuilder.Entity<LeaveRequestAuditLog>(entity =>
            {
                entity.Property(x => x.ActionType).HasMaxLength(50).IsRequired();
                entity.Property(x => x.PreviousStatus).HasMaxLength(20);
                entity.Property(x => x.NewStatus).HasMaxLength(20);
                entity.Property(x => x.Note).HasMaxLength(2000);
                entity.Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)");
                entity.HasIndex(x => x.LeaveRequestId);
                entity.HasIndex(x => x.CreatedAt);
                entity.HasOne(x => x.LeaveRequest).WithMany().HasForeignKey(x => x.LeaveRequestId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.PerformedByUser).WithMany().HasForeignKey(x => x.PerformedByUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PayrollAuditLog>(entity =>
            {
                entity.Property(x => x.EmployeeCode).HasMaxLength(50).IsRequired();
                entity.Property(x => x.EmployeeFullName).HasMaxLength(200).IsRequired();
                entity.Property(x => x.ActionType).HasMaxLength(50).IsRequired();
                entity.Property(x => x.Note).HasMaxLength(2000);
                entity.Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)");
                entity.Property(x => x.BaseSalary).HasPrecision(18, 2);
                entity.Property(x => x.Bonus).HasPrecision(18, 2);
                entity.Property(x => x.Deduction).HasPrecision(18, 2);
                entity.Property(x => x.NetSalary).HasPrecision(18, 2);
                entity.HasIndex(x => x.PayrollId);
                entity.HasIndex(x => x.CreatedAt);
                entity.HasIndex(x => new { x.EmployeeId, x.PayrollMonth, x.PayrollYear });
                entity.HasOne(x => x.PerformedByUser).WithMany().HasForeignKey(x => x.PerformedByUserId).OnDelete(DeleteBehavior.Restrict);
            });


            modelBuilder.Entity<AttendanceAdjustmentAuditLog>(entity =>
            {
                entity.Property(x => x.EmployeeCode).HasMaxLength(50).IsRequired();
                entity.Property(x => x.EmployeeFullName).HasMaxLength(200).IsRequired();
                entity.Property(x => x.RequestedStatus).HasMaxLength(20).IsRequired();
                entity.Property(x => x.CurrentStatus).HasMaxLength(20).IsRequired();
                entity.Property(x => x.ActionType).HasMaxLength(50).IsRequired();
                entity.Property(x => x.PreviousStatus).HasMaxLength(20);
                entity.Property(x => x.NewStatus).HasMaxLength(20);
                entity.Property(x => x.Note).HasMaxLength(2000);
                entity.Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)");
                entity.Property(x => x.WorkDate).HasColumnType("date");
                entity.HasIndex(x => x.AttendanceAdjustmentRequestId);
                entity.HasIndex(x => x.CreatedAt);
                entity.HasIndex(x => new { x.EmployeeId, x.WorkDate });
                entity.HasOne(x => x.AttendanceAdjustmentRequest).WithMany().HasForeignKey(x => x.AttendanceAdjustmentRequestId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.PerformedByUser).WithMany().HasForeignKey(x => x.PerformedByUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<WorkShift>(entity =>
            {
                entity.Property(x => x.ShiftCode).HasMaxLength(50).IsRequired();
                entity.Property(x => x.ShiftName).HasMaxLength(100).IsRequired();
                entity.Property(x => x.StandardHours).HasPrecision(10, 2);
                entity.Property(x => x.Notes).HasMaxLength(1000);
                entity.HasIndex(x => x.ShiftCode).IsUnique();
            });

            modelBuilder.Entity<WorkSchedule>(entity =>
            {
                entity.Property(x => x.WorkDate).HasColumnType("date");
                entity.Property(x => x.Notes).HasMaxLength(1000);
                entity.HasIndex(x => new { x.EmployeeId, x.WorkDate }).IsUnique();
                entity.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.WorkShift).WithMany().HasForeignKey(x => x.WorkShiftId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<OvertimeRequest>(entity =>
            {
                entity.Property(x => x.WorkDate).HasColumnType("date");
                entity.Property(x => x.Hours).HasPrecision(10, 2);
                entity.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
                entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
                entity.Property(x => x.ApprovalNote).HasMaxLength(1000);
                entity.Property(x => x.RejectionReason).HasMaxLength(1000);
                entity.HasIndex(x => x.EmployeeId);
                entity.HasIndex(x => x.Status);
                entity.HasIndex(x => new { x.EmployeeId, x.WorkDate, x.Status });
                entity.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.ApprovedByUser).WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_OvertimeRequests_Status", "[Status] IN ('Pending', 'Approved', 'Rejected', 'Cancelled')");
                    t.HasCheckConstraint("CK_OvertimeRequests_Hours", "[Hours] > 0");
                });
            });

            modelBuilder.Entity<LeaveBalance>(entity =>
            {
                entity.Property(x => x.AnnualAllocated).HasPrecision(10, 2);
                entity.Property(x => x.AnnualUsed).HasPrecision(10, 2);
                entity.Property(x => x.SickAllocated).HasPrecision(10, 2);
                entity.Property(x => x.SickUsed).HasPrecision(10, 2);
                entity.Property(x => x.CarryForward).HasPrecision(10, 2);
                entity.Property(x => x.UnpaidDays).HasPrecision(10, 2);
                entity.HasIndex(x => new { x.EmployeeId, x.Year }).IsUnique();
                entity.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            });

        }
    }
}
