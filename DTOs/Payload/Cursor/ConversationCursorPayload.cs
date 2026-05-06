namespace Kpett.ChatApp.DTOs.Payload.Cursor
{
    public class ConversationCursorPayload
    {
        public DateTime? LastMessageAt { get; set; }
        public string ConversationId { get; set; } = null!;
    }
}
