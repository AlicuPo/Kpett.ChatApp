using Kpett.ChatApp.Configs;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;


namespace Kpett.ChatApp.Services.Impls
{
    public class JwtService : IJwtService
    {

        private readonly IHttpContextAccessor _contextAccessor;
        private readonly JwtOptions _jwtOptions;
        private readonly IConfiguration _config;
        public JwtService(IHttpContextAccessor contextAccessor, IOptions<JwtOptions> options, IConfiguration config)
        {
            _contextAccessor = contextAccessor;
            _jwtOptions = options.Value;
            _config = config;
        }

        public string GenerateAccessToken(string userId, string email)
        {
            var jwtKey = _jwtOptions.KeyAccess;
            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new InvalidOperationException("JwtSection KeyAccess is not configured.");
            }

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            );

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var jti = Guid.NewGuid().ToString();

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.NameId, userId),
                new Claim(JwtRegisteredClaimNames.Jti, jti),
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Email, email),
            };

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpireMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        public string GenerateRefreshToken(string userId, string email)
        {
            var jwtKey = _jwtOptions.KeyRefres;
            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new InvalidOperationException("JwtSection KeyRefres is not configured.");
            }

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            );

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var jti = Guid.NewGuid().ToString();

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.NameId, userId),
                new Claim(JwtRegisteredClaimNames.Jti, jti),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.NameIdentifier, userId)
            };

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpireDays),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token, bool isRefresh = false)
        {
            var keyName = isRefresh ? "JwtSection:KeyRefres" : "JwtSection:KeyAccess";
            var jwtKey = _config[keyName];
            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new InvalidOperationException("JWT key is not configured.");
            }

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = false, // ignore exp
                ValidIssuer = _jwtOptions.Issuer,
                ValidAudience = _jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey)
                )
            };
            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out _);

            return principal;
        }

        public UserClaims? GetUserClaims()
        {
            try
            {
                if (_contextAccessor.HttpContext?.User == null)
                    return null;

                var claims = _contextAccessor.HttpContext.User.Claims;

                // Extract required claims
                var userId = claims.FirstOrDefault(_ => _.Type == ClaimTypes.NameIdentifier)?.Value;
                var username = claims.FirstOrDefault(_ => _.Type == ClaimTypes.Name || _.Type == "name")?.Value;

                // Validate required claims
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
                    return null;

                // Extract optional claims
                var displayName = claims.FirstOrDefault(c => c.Type == "DisplayName")?.Value;
                var avatarUrl = claims.FirstOrDefault(c => c.Type == "AvatarUrl")?.Value;
                var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
                var jti = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

                // Extract expiration time
                DateTime? expiresAt = null;
                var expClaim = claims.FirstOrDefault(_ => _.Type == JwtRegisteredClaimNames.Exp)?.Value;
                if (long.TryParse(expClaim, out var expUnix))
                    expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;

                // Extract issued at time
                DateTime? issuedAt = null;
                var iatClaim = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat)?.Value;
                if (long.TryParse(iatClaim, out var iatUnix))
                    issuedAt = DateTimeOffset.FromUnixTimeSeconds(iatUnix).UtcDateTime;

                // Extract roles
                var roles = claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                return new UserClaims(
                    UserId: userId,
                    Username: username,
                    DisplayName: displayName,
                    AvatarUrl: avatarUrl,
                    ExpiresAt: expiresAt,
                    IssuedAt: issuedAt,
                    Roles: roles.Count > 0 ? roles : null,
                    Email: email,
                    Jti: jti
                );
            }
            catch (Exception ex)
            {
                // Log error but don't throw - return null if extraction fails
                Console.WriteLine($"Error extracting user claims: {ex.Message}");
                return null;
            }
        }
    }

}
