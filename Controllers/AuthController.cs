using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using smart_hr_attendance_payroll_management.Common;
using smart_hr_attendance_payroll_management.Data;
using smart_hr_attendance_payroll_management.DTOs;
using smart_hr_attendance_payroll_management.Entities;
using smart_hr_attendance_payroll_management.Models;
using smart_hr_attendance_payroll_management.Services;
using System.Security.Claims;

namespace smart_hr_attendance_payroll_management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly PasswordService _passwordService;
        private readonly TokenService _tokenService;
        private readonly RefreshTokenService _refreshTokenService;

        public AuthController(
            AppDbContext context,
            PasswordService passwordService,
            TokenService tokenService,
            RefreshTokenService refreshTokenService)
        {
            _context = context;
            _passwordService = passwordService;
            _tokenService = tokenService;
            _refreshTokenService = refreshTokenService;
        }

        [HttpPost("register")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Register(RegisterUserRequest request)
        {
            var username = request.Username.Trim();
            var role = request.Role.Trim();

            if (!UserRoles.All.Contains(role))
                return BadRequest("Invalid role.");

            if ((role == UserRoles.Employee || role == UserRoles.Manager) && !request.EmployeeId.HasValue)
                return BadRequest("EmployeeId is required for Employee or Manager role.");

            if ((role == UserRoles.Admin || role == UserRoles.HR) && request.EmployeeId.HasValue)
                return BadRequest("EmployeeId should only be used for Employee or Manager role.");

            var usernameExists = await _context.AppUsers
                .AnyAsync(u => u.Username == username);

            if (usernameExists)
                return BadRequest("Username already exists.");

            if (request.EmployeeId.HasValue)
            {
                var employeeExists = await _context.Employees
                    .AnyAsync(e => e.Id == request.EmployeeId.Value);

                if (!employeeExists)
                    return BadRequest("EmployeeId does not exist.");

                var employeeLinked = await _context.AppUsers
                    .AnyAsync(u => u.EmployeeId == request.EmployeeId.Value);

                if (employeeLinked)
                    return BadRequest("This employee already has an account.");
            }

            _passwordService.CreatePasswordHash(
                request.Password,
                out byte[] passwordHash,
                out byte[] passwordSalt);

            var user = new AppUser
            {
                Username = username,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                Role = role,
                EmployeeId = request.EmployeeId,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            _context.AppUsers.Add(user);
            await _context.SaveChangesAsync();
            await _refreshTokenService.RevokeAllForUserAsync(user.Id, HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Role,
                user.EmployeeId,
                user.IsActive,
                user.CreatedAt
            });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var username = request.Username.Trim();

            var user = await _context.AppUsers
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
                return BadRequest("Invalid username or password.");

            if (!user.IsActive)
                return BadRequest("This account is inactive.");

            var isPasswordValid = _passwordService.VerifyPassword(
                request.Password,
                user.PasswordHash,
                user.PasswordSalt);

            if (!isPasswordValid)
                return BadRequest("Invalid username or password.");

            var (token, expiresAt) = _tokenService.CreateToken(user);
            var jwtSettings = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtSettings>>().Value;
            var refreshToken = await _refreshTokenService.CreateAsync(user.Id, jwtSettings.RefreshTokenExpiryDays, HttpContext.Connection.RemoteIpAddress?.ToString());

            var response = new AuthResponse
            {
                Token = token,
                ExpiresAt = expiresAt,
                UserId = user.Id,
                Username = user.Username,
                Role = user.Role,
                EmployeeId = user.EmployeeId,
                RefreshToken = refreshToken.Token,
                RefreshTokenExpiresAt = refreshToken.ExpiresAt
            };

            return Ok(response);
        }


        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
        {
            var existingToken = await _refreshTokenService.GetActiveAsync(request.RefreshToken);
            if (existingToken?.User == null || !existingToken.User.IsActive)
                return BadRequest("Invalid or expired refresh token.");

            var jwtSettings = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtSettings>>().Value;
            var replacement = await _refreshTokenService.CreateAsync(existingToken.UserId, jwtSettings.RefreshTokenExpiryDays, HttpContext.Connection.RemoteIpAddress?.ToString());
            await _refreshTokenService.RevokeAsync(existingToken.Token, HttpContext.Connection.RemoteIpAddress?.ToString(), replacement.Token);
            var (token, expiresAt) = _tokenService.CreateToken(existingToken.User);

            return Ok(new AuthResponse
            {
                Token = token,
                ExpiresAt = expiresAt,
                UserId = existingToken.User.Id,
                Username = existingToken.User.Username,
                Role = existingToken.User.Role,
                EmployeeId = existingToken.User.EmployeeId,
                RefreshToken = replacement.Token,
                RefreshTokenExpiresAt = replacement.ExpiresAt
            });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest? request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            if (!string.IsNullOrWhiteSpace(request?.RefreshToken))
                await _refreshTokenService.RevokeAsync(request.RefreshToken, HttpContext.Connection.RemoteIpAddress?.ToString());
            else
                await _refreshTokenService.RevokeAllForUserAsync(userId, HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new { message = "Logged out successfully." });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userIdClaim))
                return Unauthorized();

            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var user = await _context.AppUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound();

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Role,
                user.EmployeeId,
                user.IsActive,
                user.CreatedAt
            });
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userIdClaim))
                return Unauthorized();

            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var user = await _context.AppUsers
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound("User not found.");

            var isCurrentPasswordValid = _passwordService.VerifyPassword(
                request.CurrentPassword,
                user.PasswordHash,
                user.PasswordSalt);

            if (!isCurrentPasswordValid)
                return BadRequest("CurrentPassword is incorrect.");

            _passwordService.CreatePasswordHash(
                request.NewPassword,
                out byte[] passwordHash,
                out byte[] passwordSalt);

            user.PasswordHash = passwordHash;
            user.PasswordSalt = passwordSalt;

            await _context.SaveChangesAsync();
            await _refreshTokenService.RevokeAllForUserAsync(user.Id, HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new
            {
                message = "Password changed successfully."
            });
        }
    }
}