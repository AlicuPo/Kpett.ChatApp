namespace Kpett.ChatApp.DTOs
{
    public class PostResponseDTO
    {
        public long Id { get; set; }
        public string CreatedByUserId { get; set; } = null!;
        public string CreatedByName { get; set; } = null!;
        public string? CreatedByAvatar { get; set; }
        public string? Content { get; set; }
        public string? Privacy { get; set; }
        public string? GroupId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<PostMediaDTO>? Media { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public bool IsLikedByCurrentUser { get; set; }
        public List<CommentDTO>? Comments { get; set; }
    }

    public class PostMediaDTO
    {
        public string Id { get; set; } = null!;
        public long PostId { get; set; }
        public string? MediaUrl { get; set; }
        public string? MediaType { get; set; }
        public string? ThumbnailUrl { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int? Duration { get; set; }
        public int? SortOrder { get; set; }
    }

    public class CommentDTO
    {
        public string Id { get; set; } = null!;
        public long PostId { get; set; }
        public string UserId { get; set; } = null!;
        public string UserName { get; set; } = null!;
        public string? UserAvatar { get; set; }
        public string? Content { get; set; }
        public string? ParentCommentId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<CommentDTO>? RepliesComments { get; set; }
    }

    public class UserFeedDTO
    {
        public string Id { get; set; } = null!;
        public string UserId { get; set; } = null!;
        public long PostId { get; set; }
        public string? SourceUserId { get; set; }
        public string? SourceUserName { get; set; }
        public string? SourceType { get; set; }
        public DateTime? CreatedAt { get; set; }
        public PostResponseDTO? Post { get; set; }
    }

    public class PostReactionDTO
    {
        public long Id { get; set; }
        public long PostId { get; set; }
        public string UserId { get; set; } = null!;
        public byte? Type { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
