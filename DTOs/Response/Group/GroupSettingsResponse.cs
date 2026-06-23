namespace Kpett.ChatApp.DTOs.Response.Group
{
    public class GroupSettingsResponse
    {
        public string GroupId { get; set; } = string.Empty;

        public string Privacy { get; set; } = "public";

        public string WhoCanPost { get; set; } = "anyone";

        public string WhoCanInvite { get; set; } = "anyone";

        public bool PostApproval { get; set; }

        public bool MemberApproval { get; set; }

        public string Language { get; set; } = "vi";

        public List<GroupRuleResponse> Rules { get; set; } = new();

        public DateTime? UpdatedAt { get; set; }
    }
}
