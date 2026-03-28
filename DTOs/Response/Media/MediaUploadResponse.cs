namespace Kpett.ChatApp.DTOs.Response.Media
{
    public class MediaUploadResponse
    {
        public string PublicId { get; set; } = string.Empty;
        public string SecureUrl { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public long Bytes { get; set; }
    }
}
