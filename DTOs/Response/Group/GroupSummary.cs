using Kpett.ChatApp.Enums;

namespace Kpett.ChatApp.DTOs.Response.Group
{
    public class GroupSummary
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Slug { get; set; }
        public string? AvatarUrl { get; set; }
        public GroupPrivacy Privacy { get; set; }
        public int MemberCount { get; set; }
        public bool IsMember { get; set; }
    }
}
