namespace Kpett.ChatApp.DTOs.Response.Friend
{
    public class CreateFriendRequestResult
    {
        public FriendRequestResponse FriendRequest { get; set; } = null!;
        public bool IsCreated { get; set; }
    }
}
