namespace Kpett.ChatApp.DTOs.Payload.Cursor
{
    public class MessageCursorPayload
    {
        public string MessageId { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
