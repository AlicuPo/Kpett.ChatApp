namespace Kpett.ChatApp.DTOs.Response.Group
{
    public class GroupMembershipActionResponse
    {
        public string GroupId { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string Role { get; set; } = "member";

        public bool RequiresApproval { get; set; }

        public DateTime? JoinedAt { get; set; }
    }
}
