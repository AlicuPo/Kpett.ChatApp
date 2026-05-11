namespace Kpett.ChatApp.DTOs.Request.Friend
{
    public class FriendListRequest
    {
        public string? Search { get; set; }
        public string? Cursor { get; set; }
        public int Limit { get; set; } = 20;
    }
}
