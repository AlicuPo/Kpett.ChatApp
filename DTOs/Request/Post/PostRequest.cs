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
        public long Id { get; set; }
        public string? UserId { get; set; }
        public long PostId { get; set; }
        public string? SourceUserId { get; set; }
        public string? SourceType { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
