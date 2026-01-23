using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Kpett.ChatApp.Helper;


namespace Kpett.ChatApp.Reposoitory
{
    public class TokenRespository : IToken
    {

        private readonly AppDbContext _configuration;
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly IConfiguration _config;
        public TokenRespository(IHttpContextAccessor contextAccessor, AppDbContext configuration, IConfiguration config)
        {
            _contextAccessor = contextAccessor;
            _configuration = configuration;
            _config = config;
        }

        public string GenerateAccessToken(string userId , string UserName)
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
                new Claim(JwtRegisteredClaimNames.Name,UserName),
                new Claim(ClaimTypes.NameIdentifier, userId)
            };

            var token = new JwtSecurityToken(
                issuer: _config["JwtSection:Issuer"],
                audience: _config["JwtSection:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        public string GenerateRefreshToken(string userId, string UserName)
        {
            var jwtKey = _config["JwtSection:KeyRefres"];
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
                new Claim(JwtRegisteredClaimNames.Name,UserName),
                new Claim(ClaimTypes.NameIdentifier, userId)
            };

            var token = new JwtSecurityToken(
                issuer: _config["JwtSection:Issuer"],
                audience: _config["JwtSection:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(30),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var jwtKey = _config["JwtSection:Key"];
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
            if (_contextAccessor.HttpContext?.User == null)
                return null;

            var claims = _contextAccessor.HttpContext.User.Claims;

            var userId = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            var username = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

            var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

            DateTime? exp = null;
            var expClaim = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp)?.Value;
            if (long.TryParse(expClaim, out var expUnix)) exp = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;

            DateTime? iat = null;
            var iatClaim = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat)?.Value;
            if (long.TryParse(iatClaim, out var iatUnix)) iat = DateTimeOffset.FromUnixTimeSeconds(iatUnix).UtcDateTime;

            if (userId == null || username == null || email == null)
                return null;

            return new UserClaims(
               userId,
               username,
               email,
               claims.FirstOrDefault(c => c.Type == "DisplayName")?.Value,
               claims.FirstOrDefault(c => c.Type == "AvatarUrl")?.Value,
               exp,
               iat,
               claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList());
        }
    }

}
