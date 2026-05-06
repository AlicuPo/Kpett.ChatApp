using Kpett.ChatApp.DTOs.Response.Conversation.Metadata;

namespace Kpett.ChatApp.DTOs.Response.Conversation
{
    public class MessageResponse
    {
        public string Id { get; set; } = null!;
        public string ConversationId { get; set; } = null!;
        public string? ClientMessageId { get; set; }
        public string SenderId { get; set; } = null!;
        public string SenderName { get; set; } = null!;
        public string? SenderAvatarUrl { get; set; }
        public string Type { get; set; } = null!;
        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public SystemMessageMetadata? ActionMetadata { get; set; }
        public string? ReplyToMessageId { get; set; }
        public List<MessageAttachmentResponse>? Attachments { get; set; }

    }
}
