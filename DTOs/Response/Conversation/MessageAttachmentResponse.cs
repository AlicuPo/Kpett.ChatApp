namespace Kpett.ChatApp.DTOs.Response.Conversation
{
    public class MessageAttachmentResponse
    {
        public string Id { get; set; } = null!;
        public string MessageId { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string Url { get; set; } = null!;
        public string? PublicId { get; set; }
        public string? Filename { get; set; }
        public long? FileSize { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
    }
}
