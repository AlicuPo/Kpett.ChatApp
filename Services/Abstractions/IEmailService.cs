namespace Kpett.ChatApp.Services.Abstractions
{
    /// <summary>
    /// Service gửi email.
    /// </summary>
    public interface IEmailService
    {
        /// <summary>Gửi OTP đặt lại mật khẩu qua email.</summary>
        Task SendPasswordResetOtpAsync(string toEmail, string otp, TimeSpan lifetime, CancellationToken cancel = default);
    }
}


