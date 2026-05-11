namespace Kpett.ChatApp.DTOs.Payload.Cursor
{
    public class BaseCursorPayload
    {
        public string Id { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
    }
}
