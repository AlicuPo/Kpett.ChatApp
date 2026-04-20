namespace Kpett.ChatApp.DTOs.Response.User
{
    public class ProfileViewerContext
    {
        public bool IsOwner { get; set; }
        public bool IsFriend { get; set; }
        public bool IsFollowing { get; set; }
        public string? RelationshipRequestId { get; set; }
        public bool HasSentFriendRequest { get; set; }
        public bool HasReceivedFriendRequest { get; set; }
        public bool IsBlocked { get; set; }
        public bool CanMessage { get; set; }
    }
}
