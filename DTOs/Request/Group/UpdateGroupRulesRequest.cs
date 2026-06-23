namespace Kpett.ChatApp.DTOs.Request.Group
{
    public class UpdateGroupRulesRequest
    {
        public List<UpsertGroupRuleRequest> Rules { get; set; } = new();
    }
}
