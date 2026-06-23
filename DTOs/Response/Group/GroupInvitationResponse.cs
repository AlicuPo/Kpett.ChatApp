namespace Kpett.ChatApp.DTOs.Response.Group
{
    public class GroupInvitationResponse
    {
        public string Id { get; set; } = string.Empty;

        public string GroupId { get; set; } = string.Empty;

        public string InvitedByUserId { get; set; } = string.Empty;

        public string InviteeUserId { get; set; } = string.Empty;

        public string Status { get; set; } = "pending";

        public DateTime CreatedAt { get; set; }
    }
}
