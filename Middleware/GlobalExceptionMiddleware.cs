using System.Text.Json;

namespace smart_hr_attendance_payroll_management.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception for {Method} {Path}. TraceId={TraceId}", context.Request.Method, context.Request.Path, context.TraceIdentifier);
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/problem+json";
                var payload = new
                {
                    type = "https://httpstatuses.com/500",
                    title = "An unexpected server error occurred.",
                    status = 500,
                    traceId = context.TraceIdentifier,
                    detail = ex.Message
                };
                await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
            }
        }
    }
}
