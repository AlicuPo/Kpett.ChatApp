using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Response;
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
            _logger.LogError(exception, "Exception occurred: {Message}", exception.Message);

            var statusCode = (int)HttpStatusCode.InternalServerError;
            var errorCode = ErrorCodes.SERVER.SYSTEM_ERROR;
            var message = "An unexpected error occurred. Please try again later.";

            if (exception is AppException appEx)
            {
                statusCode = appEx.StatusCode;
                errorCode = appEx.ErrorCode;
                message = appEx.Message;
            }

            var response = new ErrorResponse() {
                ErrorCode = errorCode,
                StatusCode = statusCode,
                Message = message,
                StackTrace = _env.IsDevelopment() ? exception.StackTrace : null
            };

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/json";

            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

            return true;
        }
    }

}
