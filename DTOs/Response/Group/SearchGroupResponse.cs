namespace Kpett.ChatApp.DTOs.Response.Group
{
    public class SearchGroupResponse
    {
        public List<GroupSummary> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
