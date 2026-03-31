namespace smart_hr_attendance_payroll_management.DTOs
{
    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }

        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

        public int? EmployeeId { get; set; }

        public string RefreshToken { get; set; } = string.Empty;
        public DateTime? RefreshTokenExpiresAt { get; set; }
    }
}