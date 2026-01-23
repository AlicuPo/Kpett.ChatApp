namespace Kpett.ChatApp.DTOs.Response
{
    public class ConversationResponse
    {
        public string Id { get; set; }
        public string? Name { get; set; } // Tên nhóm hoặc tên người chat cùng
        public string? AvatarUrl { get; set; }
        public bool? IsGroup { get; set; }
        public string? LastMessageContent { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public string? OtherUserId { get; set; } // Dùng cho chat 1-1
    }
}
