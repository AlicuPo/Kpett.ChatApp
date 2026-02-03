namespace Kpett.ChatApp.DTOs
{
    public class MessageDTO
    {
        public long? Id { get; set; }
        public string? Content { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? SenderId { get; set; }
        public string? Type { get; set; } 
        public string? Metadata { get; set; }
    }
    public class LastMessageDto
    {
        public string? Content { get; set; }
        public string? SenderId { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
    public class MessagePageResult
    {
        public List<MessageDTO>? Messages { get; set; }
         public long? OldestMessageId { get; set; }
        public bool? HasMore { get; set; }
    }
    
}
