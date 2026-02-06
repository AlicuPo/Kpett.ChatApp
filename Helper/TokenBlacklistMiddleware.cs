using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Kpett.ChatApp.Services;

namespace Kpett.ChatApp.Helper
{
    /// <summary>
    /// Middleware to validate that access tokens have not been blacklisted.
    /// Checks the JTI (JWT ID) claim against the Redis blacklist.
    /// </summary>
    public class TokenBlacklistMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TokenBlacklistMiddleware> _logger;

        public TokenBlacklistMiddleware(RequestDelegate next, ILogger<TokenBlacklistMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IRedis redis)
        {
            try
            {
                // Only check if user is authenticated
                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    // Extract JTI from claims
                    var jtiClaim = context.User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

                    if (!string.IsNullOrEmpty(jtiClaim))
                    {
                        // Check if token is blacklisted
                        var isBlacklisted = await redis.IsAccessTokenBlacklistedAsync(jtiClaim);
                        if (isBlacklisted)
                        {
                            _logger.LogWarning($"Access attempt with blacklisted token JTI: {jtiClaim}");
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync("{\"message\": \"Token has been revoked.\"}");
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in token blacklist middleware: {ex.Message}");
                // Continue processing even if blacklist check fails
            }

            await _next(context);
        }
    }

    /// <summary>
    /// Extension method to register TokenBlacklistMiddleware
    /// </summary>
    public static class TokenBlacklistMiddlewareExtensions
    {
        public static IApplicationBuilder UseTokenBlacklistMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TokenBlacklistMiddleware>();
        }
    }
}
