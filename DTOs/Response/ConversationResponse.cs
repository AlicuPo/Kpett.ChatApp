namespace Kpett.ChatApp.DTOs.Response
{
    public class ConversationResponse
    {
        public string? Id { get; set; } 
        public string? Name { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Type { get; set; }
        public DateTime? LastMessageAt { get; set; }
        // Thông tin tin nhắn cuối cùng
        public LastMessageDto? LastMessage { get; set; }
        // Số tin nhắn chưa đọc
        public int UnreadCount { get; set; }
    }
}
