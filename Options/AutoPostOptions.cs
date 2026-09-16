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
        /// Hướng giải trí/hài/đời sống + pet, tránh chính trị/thời sự:
        /// giai-tri (showbiz giật gân), cuoi (hài), tam-su (tâm sự), doi-song + gia-dinh (đời sống/đồ ăn),
        /// du-lich (ẩm thực/du lịch), Google News (tin thú cưng/đồ ăn pet tiếng Việt).
        /// </summary>
        public List<string> RssFeeds { get; set; } = new()
        {
            "https://vnexpress.net/rss/giai-tri.rss",
            "https://vnexpress.net/rss/cuoi.rss",
            "https://vnexpress.net/rss/tam-su.rss",
            "https://vnexpress.net/rss/doi-song.rss",
            "https://vnexpress.net/rss/gia-dinh.rss",
            "https://vnexpress.net/rss/du-lich.rss",
            "https://news.google.com/rss/search?q=th%C3%BA%20c%C6%B0ng%20ch%C3%B3%20m%C3%A8o%20th%E1%BB%A9c%20%C4%83n%20pet&hl=vi&gl=VN&ceid=VN:vi"
        };

        /// <summary>
        /// Từ khóa chính trị/thời sự cần loại bỏ kể cả khi lọt từ feed giải trí/đời sống.
        /// So khớp không dấu, chứa một trong các từ này trong tiêu đề + mô tả thì skip.
        /// </summary>
        public List<string> BlockedKeywords { get; set; } = new()
        {
            "quoc hoi", "chinh phu", "thu tuong", "chu tich nuoc", "bo chinh tri",
            "bau cu", "dang cong san", "quoc phong", "chien su", "xung dot vu trang",
            "ukraine", "nga - ukraine", "israel", "hamas", "dai loan", "bien dong",
            "trung quoc", "my - trung", "tong thong trump", "tong thong putin",
            "nghi quyet", "ky hop", "dai bieu quoc hoi", "bo ngoai giao", "lenh trung phat",
            "thue quan", "chien tranh thuong mai"
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
