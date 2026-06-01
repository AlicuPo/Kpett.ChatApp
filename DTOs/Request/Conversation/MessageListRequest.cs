namespace Kpett.ChatApp.DTOs.Request.Conversation
{
    public class MessageListRequest
    {
        public int Limit { get; set; } = 20;
        public string? Cursor { get; set; }
    }
}
