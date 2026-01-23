namespace Kpett.ChatApp.DTOs.Request
{
    public class MessageRequest
    {
        public string? ConversationId { get; set; }
        public long? cursorMessageId { get; set; }
        public int? MessageType { get; set; } = 1;
    }
    public class CreateOneToOneChatRequest
    {
        public string? TargetUserId { get; set; }
    }
}
