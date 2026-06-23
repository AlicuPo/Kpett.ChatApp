namespace Kpett.ChatApp.DTOs.Request.Group
{
    public class InviteGroupMembersRequest
    {
        public List<string> UserIds { get; set; } = new();
    }
}
