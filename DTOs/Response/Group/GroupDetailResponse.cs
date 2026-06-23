using Kpett.ChatApp.Enums;

namespace Kpett.ChatApp.DTOs.Response.Group
{
    public class GroupDetailResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? Description { get; set; }
        public string Type { get; set; } = "public";
        public string Language { get; set; } = "vi";
        public string WhoCanPost { get; set; } = "anyone";
        public string WhoCanInvite { get; set; } = "anyone";
        public bool PostApproval { get; set; }
        public bool MemberApproval { get; set; }
        public List<GroupRuleResponse> Rules { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public string? CreatedByUserId { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Context của user đang gọi
        public bool IsMember { get; set; }
        public string? MyRole { get; set; } // "admin" | "moderator" | "member" | null
        public int MemberCount { get; set; }
    }
}
