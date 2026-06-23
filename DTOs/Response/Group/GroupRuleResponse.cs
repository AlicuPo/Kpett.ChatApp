namespace Kpett.ChatApp.DTOs.Response.Group
{
    public class GroupRuleResponse
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int Order { get; set; }
    }
}
