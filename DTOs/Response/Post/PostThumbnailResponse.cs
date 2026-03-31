using Kpett.ChatApp.DTOs.Response.Media;

namespace Kpett.ChatApp.DTOs.Response.Post
{
    public class PostThumbnailResponse: PostFeedResponse
    {
        public MediaPostResponse? MediaThumbnail { get; set; }
    }
}
