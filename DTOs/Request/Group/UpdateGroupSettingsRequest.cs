namespace Kpett.ChatApp.DTOs.Request.Group
{
    public class UpdateGroupSettingsRequest
    {
        public string? Privacy { get; set; }

        public string? WhoCanPost { get; set; }

        public string? WhoCanInvite { get; set; }

        public bool? PostApproval { get; set; }

        public bool? MemberApproval { get; set; }

        public string? Language { get; set; }

        public List<UpsertGroupRuleRequest>? Rules { get; set; }
    }
}
