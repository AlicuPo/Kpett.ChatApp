using Kpett.ChatApp.Constants;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.DTOs.Response.Shared;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

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

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/json";

            await httpContext.Response.WriteAsJsonAsync(response, jsonOptions, cancellationToken);

            return true;
        }
    }

}

