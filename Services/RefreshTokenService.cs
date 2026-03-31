using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using smart_hr_attendance_payroll_management.Data;
using smart_hr_attendance_payroll_management.Entities;

namespace smart_hr_attendance_payroll_management.Services
{
    public class RefreshTokenService
    {
        private readonly AppDbContext _context;

        public RefreshTokenService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RefreshToken> CreateAsync(int userId, int expiryDays, string? createdByIp = null)
        {
            var refreshToken = new RefreshToken
            {
                UserId = userId,
                Token = GenerateToken(),
                ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
                CreatedByIp = createdByIp,
                CreatedAt = DateTime.UtcNow
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();
            return refreshToken;
        }

        public Task<RefreshToken?> GetActiveAsync(string token)
            => _context.RefreshTokens.Include(x => x.User).FirstOrDefaultAsync(x => x.Token == token && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow);

        public async Task RevokeAsync(string token, string? ipAddress = null, string? replacedByToken = null)
        {
            var refreshToken = await _context.RefreshTokens.FirstOrDefaultAsync(x => x.Token == token);
            if (refreshToken == null || refreshToken.RevokedAt != null) return;
            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.RevokedByIp = ipAddress;
            refreshToken.ReplacedByToken = replacedByToken;
            await _context.SaveChangesAsync();
        }

        public async Task RevokeAllForUserAsync(int userId, string? ipAddress = null)
        {
            var tokens = await _context.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAt == null).ToListAsync();
            if (!tokens.Any()) return;
            foreach (var token in tokens)
            {
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedByIp = ipAddress;
            }
            await _context.SaveChangesAsync();
        }

        private static string GenerateToken()
        {
            var randomBytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(randomBytes);
        }
    }
}
