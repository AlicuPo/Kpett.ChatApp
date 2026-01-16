namespace Kpett.ChatApp.DTOs
{
    public class ChatMessageDto
    {
        public string? userName { get; set; }
        public string? Description { get; set; }
        public DateTime? CreateAt { get; set; } = DateTime.UtcNow;
        public string? userId { get; set; }
    }
}
