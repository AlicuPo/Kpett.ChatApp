namespace Kpett.ChatApp.DTOs.Response.Group
{
    public class MyGroupsResponse
    {
        public List<MyGroupItem> Items { get; set; } = new();
        public int TotalCount { get; set; }
    }
}
