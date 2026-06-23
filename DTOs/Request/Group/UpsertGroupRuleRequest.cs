namespace Kpett.ChatApp.DTOs.Request.Group
{
    public class UpsertGroupRuleRequest
    {
        public string? Title { get; set; }

        public string? Description { get; set; }

        public int? Order { get; set; }
    }
}
