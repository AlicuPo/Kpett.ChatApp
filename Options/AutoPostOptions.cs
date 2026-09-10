namespace Kpett.ChatApp.Options
{
    public class AutoPostOptions
    {
        /// <summary>
        /// Bật/tắt auto-post. Mặc định false để không tự chạy khi chưa cấu hình Bot.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// UserId sẽ đứng tên bài post. Đổi ID này là đổi người đăng.
        /// Nếu để trống sẽ fallback sang BotUsername/BotEmail.
        /// </summary>
        public string? BotUserId { get; set; }

        /// <summary>
        /// Fallback: tìm user theo username nếu BotUserId trống.
        /// </summary>
        public string BotUsername { get; set; } = "kpett_official";

        /// <summary>
        /// Fallback: tìm user theo email nếu BotUserId + Username không tìm thấy.
        /// </summary>
        public string BotEmail { get; set; } = "bot@kpett.local";

        /// <summary>
        /// Cron expression cho Hangfire. Mặc định mỗi giờ.
        /// </summary>
        public string Cron { get; set; } = CronExpressions.Hourly;

        /// <summary>
        /// Số bài tối đa tạo mỗi lần chạy job.
        /// </summary>
        public int MaxPostsPerRun { get; set; } = 2;

        /// <summary>
        /// Danh sách RSS feed để crawl. Đổi list này không cần sửa code.
        /// </summary>
        public List<string> RssFeeds { get; set; } = new()
        {
            "https://vnexpress.net/rss/tin-moi-nhat.rss",
            "https://vnexpress.net/rss/the-gioi.rss"
        };
    }

    public static class CronExpressions
    {
        public const string Every30Minutes = "*/30 * * * *";
        public const string Hourly = "0 * * * *";
        public const string Every2Hours = "0 */2 * * *";
        public const string Daily = "0 8 * * *";
    }
}
