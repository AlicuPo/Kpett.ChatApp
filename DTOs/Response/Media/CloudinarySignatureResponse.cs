namespace Kpett.ChatApp.DTOs.Response.Media
{
    public class CloudinarySignatureResponse
    {
        public string Signature { get; set; } = string.Empty;
        public long Timestamp { get; set; }
        public string CloudName { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string Folder { get; set; } = string.Empty;
        public string NotificationUrl { get; set; } = string.Empty;
    }
}
