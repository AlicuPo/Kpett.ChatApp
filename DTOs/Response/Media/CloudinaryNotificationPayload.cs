namespace Kpett.ChatApp.DTOs.Response.Media
{
    public class CloudinaryNotificationPayload
    {
        public string notification_type { get; set; } = string.Empty;

        public string public_id { get; set; } = string.Empty;
        public string asset_id { get; set; } = string.Empty;
        public string secure_url { get; set; } = string.Empty;
        public string resource_type { get; set; } = string.Empty;
        public string format { get; set; } = string.Empty;
        public long bytes { get; set; }
        public string folder { get; set; } = string.Empty;
        public DateTime created_at { get; set; }
    }
}
