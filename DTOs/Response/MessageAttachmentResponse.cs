namespace Kpett.ChatApp.DTOs.Response
{
    public class MessageAttachmentResponse
    {
        public string Type { get; set; } = null!;
        public string Url { get; set; } = null!;
        public string? PublicId { get; set; }
        public string? Filename { get; set; }
        public long? FileSize { get; set; }
    }
}
