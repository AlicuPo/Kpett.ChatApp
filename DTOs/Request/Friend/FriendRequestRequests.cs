namespace Kpett.ChatApp.DTOs.Request.Friend
{
    public class SendFriendRequestRequest
    {
        public string ReceiverId { get; set; } = null!;
    }

    public class UpdateFriendRequestStatusRequest
    {
        public string Status { get; set; } = null!;
    }
}
