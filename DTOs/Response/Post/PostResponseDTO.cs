namespace Kpett.ChatApp.DTOs.Response.Post
{
    public class PostResponseDTO
    {
        public string Id { get; set; } = null!;
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
    }

    public class PostMediaDTO
    {
        public string Id { get; set; } = null!;
        public string PostId { get; set; } = null!;
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
        public string PostId { get; set; } = null!;
        public string UserId { get; set; } = null!;
        public string UserName { get; set; } = null!;
        public string? UserAvatar { get; set; }
        public string? Content { get; set; }
        public string? ParentCommentId { get; set; }
        public int LikeCount { get; set; }
        public int ReplyCount { get; set; }
        public bool IsEdited { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<CommentMentionDTO>? Mentions { get; set; }
        public List<CommentDTO>? RepliesComments { get; set; }
    }

    public class CommentMentionDTO
    {
        public string Id { get; set; } = null!;
        public string UserId { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string? DisplayName { get; set; }
        public bool IsNotified { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CommentsPageDTO
    {
        public List<CommentListItemDTO> Items { get; set; } = new();
        public CommentPaginationDTO Pagination { get; set; } = new();
    }

    public class CommentListItemDTO
    {
        public string Id { get; set; } = null!;
        public string PostId { get; set; } = null!;
        public string? ParentId { get; set; }
        public CommentAuthorDTO Author { get; set; } = new();
        public string? Content { get; set; }
        public List<CommentMentionSummaryDTO> Mentions { get; set; } = new();
        public List<CommentAttachmentDTO> Attachments { get; set; } = new();
        public CommentMetricsDTO Metrics { get; set; } = new();
        public CommentViewerContextDTO ViewerContext { get; set; } = new();
        public bool IsEdited { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CommentAuthorDTO
    {
        public string Id { get; set; } = null!;
        public string? Username { get; set; }
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
        public bool IsVerified { get; set; }
    }

    public class CommentMentionSummaryDTO
    {
        public string UserId { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string? DisplayName { get; set; }
    }

    public class CommentAttachmentDTO
    {
        public string? Id { get; set; }
        public string? Url { get; set; }
        public string? Type { get; set; }
    }

    public class CommentMetricsDTO
    {
        public int LikeCount { get; set; }
        public int ReplyCount { get; set; }
    }

    public class CommentViewerContextDTO
    {
        public bool IsLiked { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool CanReply { get; set; }
    }

    public class CommentPaginationDTO
    {
        public string? NextCursor { get; set; }
        public bool HasMore { get; set; }
        public int Limit { get; set; }
        public int TotalCount { get; set; }
    }

    public class UserFeedDTO
    {
        public string Id { get; set; } = null!;
        public string UserId { get; set; } = null!;
        public string PostId { get; set; } = null!;
        public string? SourceUserId { get; set; }
        public string? SourceUserName { get; set; }
        public string? SourceType { get; set; }
        public DateTime? CreatedAt { get; set; }
        public PostResponseDTO? Post { get; set; }
    }

    public class PostReactionDTO
    {
        public string Id { get; set; } = null!;
        public string PostId { get; set; } = null!;
        public string UserId { get; set; } = null!;
        public byte? Type { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
