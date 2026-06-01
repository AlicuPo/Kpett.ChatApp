using Kpett.ChatApp.Constants;
using Kpett.ChatApp.DTOs.Response.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Kpett.ChatApp.Filters
{
    public class OptionalAuthorizeAttribute : TypeFilterAttribute
    {
        public OptionalAuthorizeAttribute() : base(typeof(OptionalAuthorizeFilter)) { }
    }

    public class OptionalAuthorizeFilter : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var request = context.HttpContext.Request;

            bool hasToken = request.Headers.TryGetValue("Authorization", out var authHeader)
                            && authHeader.ToString().StartsWith("Bearer ");

            if (hasToken && context.HttpContext.User?.Identity?.IsAuthenticated != true)
            {
                var errorResponse = new ErrorResponse
                {
                    ErrorCode = ErrorCodes.AUTH.ACCESS_TOKEN_INVALID,
                    StatusCode = StatusCodes.Status401Unauthorized,
                    Message = "Token invalid or expired.",
                    StackTrace = null
                };

                context.Result = new JsonResult(errorResponse)
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
            }
        }
    }
}

