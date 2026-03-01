namespace Kpett.ChatApp.DTOs
{
    public class FriendRequestDTO
    {
        public string FriendRequestId { get; set; } = null!;
        public string SenderId { get; set; } = null!;
        public string SenderName { get; set; } = null!;
        public string? SenderAvatar { get; set; }
        public string? SenderEmail { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
