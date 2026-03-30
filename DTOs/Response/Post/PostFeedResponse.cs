using Kpett.ChatApp.DTOs.Response.Media;
using Kpett.ChatApp.DTOs.Response.User;

namespace Kpett.ChatApp.DTOs.Response.Post
{
    public class PostFeedResponse
    {
        public string Id { get; set; } = string.Empty;

        public UserResponse Author { get; set; } = null!;

        public string? Title { get; set; }

        public string? Content { get; set; }

        public List<string> Hashtags { get; set; } = new List<string>();

        public List<MediaPostResponse> Media { get; set; } = new List<MediaPostResponse>();

        public PostMetricsResponse Metrics { get; set; } = null!;

        public PostViewerContextResponse ViewerContext { get; set; } = null!;

        public string? Privacy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
