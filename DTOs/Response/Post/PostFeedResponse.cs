using Kpett.ChatApp.DTOs.Response.Media;
using Kpett.ChatApp.DTOs.Response.User;

namespace Kpett.ChatApp.DTOs.Response.Post
{
    public class PostFeedResponse
    {
        public string Id { get; set; } = string.Empty;

        public UserResponse? Author { get; set; } = null!;

        public string? Title { get; set; }

        public string? Content { get; set; }

        public string Type { get; set; } = null!;

        public string? GroupId { get; set; }

        public PostGroupSummaryResponse? Group { get; set; }

        public string? Status { get; set; }

        public List<string> Hashtags { get; set; } = new List<string>();

        public List<MediaPostResponse> Media { get; set; } = new List<MediaPostResponse>();

        public PostMetricsResponse Metrics { get; set; } = null!;

        public PostViewerContextResponse ViewerContext { get; set; } = null!;

        public string? Privacy { get; set; } = string.Empty;

        public bool IsNsfw { get; set; }

        public bool AllowComments { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }

    public class PostGroupSummaryResponse
    {
        public string Id { get; set; } = string.Empty;

        public string? Name { get; set; }

        public string? AvatarUrl { get; set; }

        public string? Privacy { get; set; }
    }
}
