namespace Kpett.ChatApp.DTOs.Request.Conversation
{
    public class ConversationListRequest
    {
        public string? Search { get; set; }
        public string? Cursor { get; set; }
        public int Limit { get; set; }
    }
}
