namespace Kpett.ChatApp.DTOs.Request
{
    public class MessageRequest
    {
        public string? ConversationId { get; set; }
        public long? cursorMessageId { get; set; } 
        public string? CurrentUserId { get; set; } 
        public long? LastReadMessageId { get; set; }
    }
     
    public class ReadMessageRequest
    {
        public long LastReadMessageId { get; set; }
        public DateTime? ReadAt { get; set; }
    }
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public long? NextCursor { get; set; } // Trả về Id của tin nhắn cũ nhất trong list để Client load tiếp
        public int TotalUnread { get; set; }
        public bool HasMore { get; set; }
    }

    public class SendMessageRequest
    {
        public string Content { get; set; } = null!;
        public string? ClientMessageId { get; set; } // Để Client tránh gửi trùng (Idempotency) 
        public string? Type { get; set; } // Text, Image, v.v. 
        public string? Metadata { get; set; } // Thông tin bổ sung dưới dạng JSON
        public string? Coler { get; set; } // Màu sắc của tin nhắn  
    }
}
