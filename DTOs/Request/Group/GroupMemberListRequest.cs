namespace Kpett.ChatApp.DTOs.Request.Group
{
    public class GroupMemberListRequest
    {
        public string? Keyword { get; set; }

        public string? Role { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }
}
