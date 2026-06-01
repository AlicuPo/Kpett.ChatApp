namespace Kpett.ChatApp.DTOs.Response.Conversation
{
    /// <summary>
    /// Payload broadcast đến client khi có sự kiện typing trong conversation.
    /// </summary>
    public class TypingEventPayload
    {
        public string UserId { get; set; } = null!;
        public string? DisplayName { get; set; }
        public string? Username { get; set; }
        public string? AvatarUrl { get; set; }
        public string ConversationId { get; set; } = null!;
        public bool IsTyping { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
