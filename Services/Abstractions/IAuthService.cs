using Kpett.ChatApp.DTOs.Request.Auth;
using Kpett.ChatApp.DTOs.Response.Auth;
using System.Security.Claims;

namespace Kpett.ChatApp.Services.Abstractions
{
    /// <summary>
    /// Service xác th?c: ðãng nh?p, ðãng k?, ðãng xu?t, refresh token, quên m?t kh?u.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>Ðãng nh?p v?i email và m?t kh?u.</summary>
        Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancel = default);

        /// <summary>Ðãng k? tài kho?n m?i.</summary>
        Task<int> RegisterAsync(RegisterRequest request, CancellationToken cancel = default);

        /// <summary>Ðãng xu?t (thu h?i token).</summary>
        Task<bool> LogoutAsync(LogoutRequest logoutRequest, ClaimsPrincipal user, CancellationToken cancel = default);

        /// <summary>Làm m?i access token b?ng refresh token.</summary>
        Task<TokenResponse> RefreshTokenAsync(RefreshTokenRequest request);

        /// <summary>G?i OTP ð?t l?i m?t kh?u qua email.</summary>
        Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancel = default);

        /// <summary>Ð?t l?i m?t kh?u v?i OTP.</summary>
        Task ResetPasswordWithOtpAsync(ResetPasswordWithOtpRequest request, CancellationToken cancel = default);
    }
}


