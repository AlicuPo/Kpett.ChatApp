namespace Kpett.ChatApp.Options
{
    public class MediaOptions
    {
        public long MaxImageSizeBytes { get; set; } = 5 * 1024 * 1024;
        public long MaxVideoSizeBytes { get; set; } = 150 * 1024 * 1024;
        public string[] AllowedImageExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
        public string[] AllowedVideoExtensions { get; set; } = [".mp4", ".mov", ".webm", ".avi"];
    }
}
