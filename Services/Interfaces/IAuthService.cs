using Kpett.ChatApp.DTOs.Request.Auth;
using Kpett.ChatApp.DTOs.Response.Auth;
using System.Security.Claims;

namespace Kpett.ChatApp.Services.Interfaces
{
    /// <summary>
    /// Service xác thực: đăng nhập, đăng ký, đăng xuất, refresh token, quên mật khẩu.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>Đăng nhập với email và mật khẩu.</summary>
        Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancel = default);

        /// <summary>Đăng ký tài khoản mới.</summary>
        Task<int> RegisterAsync(RegisterRequest request, CancellationToken cancel = default);

        /// <summary>Đăng xuất (thu hồi token).</summary>
        Task<bool> LogoutAsync(LogoutRequest logoutRequest, ClaimsPrincipal user, CancellationToken cancel = default);

        /// <summary>Làm mới access token bằng refresh token.</summary>
        Task<TokenResponse> RefreshTokenAsync(RefreshTokenRequest request);

        /// <summary>Gửi OTP đặt lại mật khẩu qua email.</summary>
        Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancel = default);

        /// <summary>Đặt lại mật khẩu với OTP.</summary>
        Task ResetPasswordWithOtpAsync(ResetPasswordWithOtpRequest request, CancellationToken cancel = default);
    }
}
