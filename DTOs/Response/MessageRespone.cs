namespace Kpett.ChatApp.DTOs.Response
{
    public class MessageRespone
    {
        public List<MessageDto> Messages { get; set; }
        public long? OldestMessageId { get; set; }
    
    }
    public class MessageDto
    {
        public long? Id { get; set; }
        public string? Content { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? SenderId { get; set; }
    }

}

