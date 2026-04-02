namespace Kpett.ChatApp.DTOs.Response.Friend
{
    public class FriendListItemDTO
    {
        public string Id { get; set; } = null!;
        public string? Username { get; set; }
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
        public bool IsVerified { get; set; }
        public DateTime? FriendedAt { get; set; }
    }
}
