namespace Kpett.ChatApp.DTOs.Response.Conversation
{
    public class ConversationMessageResponse
    {
        public string Id { get; set; } = null!;
        public string SenderId { get; set; } = null!;
        public string Content { get; set; } = null!;
        public string Type { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
