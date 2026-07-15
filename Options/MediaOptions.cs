namespace Kpett.ChatApp.Options
{
    public class MediaOptions
    {
        public long MaxImageSizeBytes { get; set; } = 5242880;
        public long MaxVideoSizeBytes { get; set; } = 150 * 1024 * 1024;
        public string[] AllowedImageExtensions { get; set; } = Array.Empty<string>();
        public string[] AllowedVideoExtensions { get; set; } = Array.Empty<string>();
    }
}
