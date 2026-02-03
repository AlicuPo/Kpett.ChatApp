using Kpett.ChatApp.Models;

namespace Kpett.ChatApp.DTOs.Request
{
    public class PostFeedRequest
    {
        public long Id { get; set; }
        public string? CreatedByUserId { get; set; }
        public string? Content { get; set; }
        public string? Privacy { get; set; }
        public string? GroupId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsDeleted { get; set; }
        //public List<PostMediaRequest>? PostMedia { get; set; }


    }
    public class PostMediaRequest : Post
    {
        public string? MediaType { get; set; } // e.g., "image", "video"
        public string? MediaUrl { get; set; }
        public string? ThumbnailUrl { get; set; } // Optional, for videos
        public int? height { get; set; } = 700;
        public int? width { get; set; } = 400;

    }

    public class UserFeedRequest
    {
        public long Id { get; set; }
        public string? UserId { get; set; }
        public long PostId { get; set; }
        public string? SourceUserId { get; set; }
        public string? SourceType { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
