using Kpett.ChatApp.Models;

namespace Kpett.ChatApp.DTOs.Request.Post
{
    public class PostRequest
    {
        public string? Content { get; set; }
        public string? Privacy { get; set; }
        public string? GroupId { get; set; }
        public List<MediaRequest>? Media { get; set; }
    }

    public class UserFeedRequest
    {
        public string Id { get; set; } = null!;
        public string? UserId { get; set; }
        public string PostId { get; set; } = null!;
        public string? SourceUserId { get; set; }
        public string? SourceType { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
