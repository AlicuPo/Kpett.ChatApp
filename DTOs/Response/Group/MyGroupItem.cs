using Kpett.ChatApp.Enums;

namespace Kpett.ChatApp.DTOs.Response.Group
{
    public class MyGroupItem
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Slug { get; set; }
        public string? AvatarUrl { get; set; }
        public GroupMemberRole MyRole { get; set; }
        public int MemberCount { get; set; }
        public int UnreadPostCount { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}
