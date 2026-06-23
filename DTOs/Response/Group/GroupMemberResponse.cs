namespace Kpett.ChatApp.DTOs.Response.Group
{
    public class GroupMemberResponse
    {
        public string MemberId { get; set; } = string.Empty;

        public string GroupId { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        public string? Username { get; set; }

        public string? Email { get; set; }

        public string? DisplayName { get; set; }

        public bool IsVerified { get; set; }

        public string Role { get; set; } = "member";

        public string Status { get; set; } = "active";

        public DateTime CreatedAt { get; set; }

        public DateTime? JoinedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
