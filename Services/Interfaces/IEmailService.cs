namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendPasswordResetOtpAsync(string toEmail, string otp, TimeSpan lifetime, CancellationToken cancel = default);
    }
}
