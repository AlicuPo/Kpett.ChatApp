using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;

namespace Kpett.ChatApp.DTOs.Request
{
    public class ConversationsRequest
    {
      
        public string? Type { get; set; }
        public string? Name { get; set; }
        public string? AvatarUrl { get; set; }
        //public MessageDTO? LastMessage { get; set; }
        public int? UnreadCount { get; set; }
        public DateTime? LastReadAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
    public class ConversationKeysRequest : ConversationsRequest
    {
       
        public string? UserLow { get; set; }
        public string? UserHigh { get; set; }
    }
}



