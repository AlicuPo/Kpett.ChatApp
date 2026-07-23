using System.Net;
using System.Net.Mail;
using Kpett.ChatApp.Options;
using Kpett.ChatApp.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Kpett.ChatApp.Services.Implementations
{
    /// <summary>Service gửi email SMTP.</summary>
    public class SmtpEmailService : IEmailService
    {
        private readonly EmailOptions _options;

        /// <summary>Khởi tạo service với các dependencies.</summary>
        public SmtpEmailService(IOptions<EmailOptions> options)
        {
            _options = options.Value;
        }

        /// <inheritdoc />
        public async Task SendPasswordResetOtpAsync(string toEmail, string otp, TimeSpan lifetime, CancellationToken cancel = default)
        {
            cancel.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(_options.SmtpUsername)
                || string.IsNullOrWhiteSpace(_options.SmtpPassword)
                || string.IsNullOrWhiteSpace(_options.FromEmail))
            {
                throw new InvalidOperationException("SMTP settings are not configured for password reset emails.");
            }

            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromEmail, _options.FromName),
                Subject = "Kpett.ChatApp password reset OTP",
                Body = BuildBody(otp, lifetime),
                IsBodyHtml = false
            };

            message.To.Add(toEmail);

            using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                EnableSsl = _options.EnableSsl,
                Credentials = new NetworkCredential(_options.SmtpUsername, _options.SmtpPassword)
            };

            await client.SendMailAsync(message, cancel);
        }

        private static string BuildBody(string otp, TimeSpan lifetime)
        {
            return $@"Use the OTP below to reset your Kpett.ChatApp password.

OTP: {otp}
Expires in: {(int)Math.Ceiling(lifetime.TotalMinutes)} minutes

If you did not request a password reset, you can ignore this email.";
        }
    }
}
