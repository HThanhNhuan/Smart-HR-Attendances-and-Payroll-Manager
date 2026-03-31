using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using smart_hr_attendance_payroll_management.Data;
using smart_hr_attendance_payroll_management.Entities;

namespace smart_hr_attendance_payroll_management.Middleware
{
    public class RequestAuditMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestAuditMiddleware> _logger;

        public RequestAuditMiddleware(RequestDelegate next, ILogger<RequestAuditMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
        {
            var method = context.Request.Method.ToUpperInvariant();
            var shouldAudit = method is "POST" or "PUT" or "PATCH" or "DELETE";
            string? body = null;

            if (shouldAudit && context.Request.ContentLength is > 0 && context.Request.Body.CanRead)
            {
                context.Request.EnableBuffering();
                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
                body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;
                if (body?.Length > 4000) body = body[..4000];
            }

            await _next(context);

            if (!shouldAudit) return;

            try
            {
                var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                int? userId = int.TryParse(userIdClaim, out var parsed) ? parsed : null;
                var path = context.Request.Path.Value ?? string.Empty;
                var entityName = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault() ?? "Unknown";
                var entityId = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

                dbContext.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = method,
                    EntityName = entityName,
                    EntityId = entityId != entityName ? entityId : null,
                    NewValue = body,
                    HttpMethod = method,
                    Path = path,
                    StatusCode = context.Response.StatusCode,
                    TraceId = context.TraceIdentifier,
                    CreatedAt = DateTime.UtcNow
                });
                await dbContext.SaveChangesAsync();

                _logger.LogInformation("Audited {Method} {Path} -> {StatusCode} (UserId={UserId}, TraceId={TraceId})", method, path, context.Response.StatusCode, userId, context.TraceIdentifier);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist audit log for {Method} {Path}", method, context.Request.Path);
            }
        }
    }
}
