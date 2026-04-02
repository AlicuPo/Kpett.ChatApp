namespace Kpett.ChatApp.DTOs.Response.Friend
{
    public class CreateFriendRequestResult
    {
        public FriendRequestDTO FriendRequest { get; set; } = null!;
        public bool IsCreated { get; set; }
    }
}
