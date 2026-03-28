namespace Kpett.ChatApp.DTOs.Request.Post
{
    public class UpsertReactionRequest
    {
        public byte ReactionType { get; set; }
    }

    public class CreateCommentRequest
    {
        public string Content { get; set; } = null!;
        public string? ParentCommentId { get; set; }
        public List<string>? Mentions { get; set; }
    }

    public class UpdateCommentRequest
    {
        public string Content { get; set; } = null!;
        public List<string>? Mentions { get; set; }
    }
}
