namespace Kpett.ChatApp.DTOs.Response.Message
{
    public class MessageDto
    {
        public string? Id { get; set; }
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
        public List<MessageDto>? Messages { get; set; }
        public string? OldestMessageId { get; set; }
        public bool? HasMore { get; set; }
    }

}
