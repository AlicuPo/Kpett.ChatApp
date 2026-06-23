namespace Kpett.ChatApp.DTOs.Response.Group
{
    public class GroupInviteMembersResponse
    {
        public List<GroupInvitationResponse> Invitations { get; set; } = new();

        public List<GroupInviteSkippedResponse> Skipped { get; set; } = new();

        public int InvitedCount => Invitations.Count;

        public int SkippedCount => Skipped.Count;
    }
}
