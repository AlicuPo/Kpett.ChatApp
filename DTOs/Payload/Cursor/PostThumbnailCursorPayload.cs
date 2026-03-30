namespace Kpett.ChatApp.DTOs.Payload.Cursor
{
    public class PostThumbnailCursorPayload: PostFeedCursorPayload
    {
        public DateTime PinnedAt { get; set; }
    }
}
