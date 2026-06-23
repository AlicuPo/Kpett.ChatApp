namespace Kpett.ChatApp.DTOs.Response.Group
{
    public class GroupMemberListResponse
    {
        public List<GroupMemberResponse> Items { get; set; } = new();

        public int TotalCount { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }
}
