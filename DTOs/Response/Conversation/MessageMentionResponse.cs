namespace Kpett.ChatApp.DTOs.Response.Conversation
{
    public class MessageMentionResponse
    {
        public string UserId { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string? DisplayName { get; set; }
    }
}