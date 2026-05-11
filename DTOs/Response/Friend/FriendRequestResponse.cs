namespace Kpett.ChatApp.DTOs.Response.Friend
{
    public class FriendRequestResponse
    {
        public string RequestId { get; set; } = null!;
        public string SenderId { get; set; } = null!;
        public string ReceiverId { get; set; } = null!;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
