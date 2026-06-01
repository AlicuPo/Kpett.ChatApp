namespace Kpett.ChatApp.Options
{
    public class EmailOptions
    {
        public string SmtpHost { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string SmtpUsername { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = "Kpett.ChatApp";
        public int PasswordResetOtpExpiryMinutes { get; set; } = 10;
        public int PasswordResetOtpLength { get; set; } = 6;
    }
}
