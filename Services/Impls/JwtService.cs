using Kpett.ChatApp.Models;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Services.Interfaces;


namespace Kpett.ChatApp.Services.Impls
{
    public class JwtService : IJwtService
    {

        private readonly AppDbContext _configuration;
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly IConfiguration _config;
        public JwtService(IHttpContextAccessor contextAccessor, AppDbContext configuration, IConfiguration config)
        {
            _contextAccessor = contextAccessor;
            _configuration = configuration;
            _config = config;
        }

        public string GenerateAccessToken(string userId, string UserName, string? email = null, string? displayName = null)
        {
            var jwtKey = _config["JwtSection:KeyAccess"];
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
                new Claim(JwtRegisteredClaimNames.Name, UserName),
                new Claim(ClaimTypes.NameIdentifier, userId),
            };

            // Add optional claims
            if (!string.IsNullOrEmpty(email))
                claims.Add(new Claim(ClaimTypes.Email, email));

            if (!string.IsNullOrEmpty(displayName))
                claims.Add(new Claim("DisplayName", displayName));

            var token = new JwtSecurityToken(
                issuer: _config["JwtSection:Issuer"],
                audience: _config["JwtSection:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        public string GenerateRefreshToken(string userId, string UserName, string? email = null)
        {
            var jwtKey = _config["JwtSection:KeyRefres"];
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
                new Claim(JwtRegisteredClaimNames.Name, UserName),
                new Claim(ClaimTypes.NameIdentifier, userId)
            };

            // Add optional email claim
            if (!string.IsNullOrEmpty(email))
                claims.Add(new Claim(ClaimTypes.Email, email));

            var token = new JwtSecurityToken(
                issuer: _config["JwtSection:Issuer"],
                audience: _config["JwtSection:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(30),
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
                ValidIssuer = _config["JwtSection:Issuer"],
                ValidAudience = _config["JwtSection:Audience"],
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
