using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Kpett.ChatApp.Helper
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IHostEnvironment _env;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

            // 1. Phân loại mã lỗi
            var (statusCode, title) = exception switch
            {
                AppException appEx => (appEx.StatusCode, "Application Error"), 
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
                KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
                _ => (StatusCodes.Status500InternalServerError, "Server Error")
            };

            // 2. Tạo cấu trúc trả về chuẩn RFC 7807 (Problem Details)
            var problemDetails = new ProblemDetails 
            {
                Status = statusCode,
                Title = title,
                Detail = _env.IsDevelopment() ? exception.Message : "An unexpected error occurred.",
                Instance = httpContext.Request.Path
            };

            httpContext.Response.StatusCode = statusCode;

            // 3. Trả về kết quả JSON
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            // Trả về true để báo hiệu lỗi đã được xử lý xong
            return true;
        }
    }

}
