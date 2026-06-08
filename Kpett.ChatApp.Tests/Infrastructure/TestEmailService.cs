using Kpett.ChatApp.Services.Interfaces;

namespace Kpett.ChatApp.Tests.Infrastructure;

public class TestEmailService : IEmailService
{
    private readonly List<SentPasswordResetOtpEmail> _sentEmails = new();

    public IReadOnlyList<SentPasswordResetOtpEmail> SentEmails => _sentEmails;

    public Task SendPasswordResetOtpAsync(string toEmail, string otp, TimeSpan lifetime, CancellationToken cancel = default)
    {
        _sentEmails.Add(new SentPasswordResetOtpEmail(toEmail, otp, lifetime));
        return Task.CompletedTask;
    }
}

public sealed record SentPasswordResetOtpEmail(string ToEmail, string Otp, TimeSpan Lifetime);
