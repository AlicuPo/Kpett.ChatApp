namespace Kpett.ChatApp.DTOs.Request.Conversation
{
    public class SendMessageRequest
    {
        public string ClientMessageId { get; set; } = null!;

        public string? Content { get; set; }

        public string Type { get; set; } = "Text";

        public string? ReplyToMessageId { get; set; }

        public List<MessageAttachmentRequest>? Attachments { get; set; }
    }
}
