namespace Kpett.ChatApp.DTOs.Response.Post
{
    public class PostViewerContextResponse
    {
        public bool IsOwner { get; set; }
        public bool IsLiked { get; set; }
        public byte? ReactionType { get; set; }
        public bool IsSaved { get; set; }
        public bool IsPinned { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool CanLike { get; set; }
        public bool CanComment { get; set; }
        public bool? CanPin { get; set; }
    }
}
