using Azure.Core;
using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;

namespace Kpett.ChatApp.Services
{
    public interface IPostFeedService
    {
        Task PostFeed(PostMediaRequest postRequest, CancellationToken cancel);

    }
    public class PostFeedServiceImpl : IPostFeedService
    {
        private readonly AppDbContext _dbContext;
        public PostFeedServiceImpl(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task PostFeed(PostMediaRequest postRequest, CancellationToken cancel)
        {
            // Implementation for posting feed
            if (postRequest == null)
                throw new AppException(StatusCodes.Status400BadRequest, "Post request cannot be null");

            var newPost = new Post
            {
                CreatedByUserId = postRequest.CreatedByUserId,
                Content = postRequest.Content,
                Privacy = postRequest.Privacy,
                GroupId = postRequest.GroupId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false,

            };
            await _dbContext.Posts.AddAsync(newPost, cancel);
            await _dbContext.SaveChangesAsync(cancel);

            if (postRequest.MediaType == null)
            {
                return;
            }
            var postMedia = new PostMedia
            {
                Id = Guid.NewGuid().ToString(),
                PostId = newPost.Id,
                MediaType = postRequest.MediaType,
                MediaUrl = postRequest.MediaUrl,
                ThumbnailUrl = postRequest.ThumbnailUrl,
                Height = postRequest.height,
                Width = postRequest.width
            };

            cancel.ThrowIfCancellationRequested();

            await _dbContext.PostMedia.AddAsync(postMedia, cancel);
            await _dbContext.SaveChangesAsync(cancel);

        }
    }
}
//if (!string.IsNullOrEmpty(postRequest.Privacy) && Enum.TryParse<PostPrivacy>(postRequest.Privacy, out var insight))
//    switch (insight)
//    {
//        case PostPrivacy.Public:
//            postRequest.Privacy = "Public";
//            break;
//        case PostPrivacy.Friends:
//            postRequest.Privacy = "Friends";
//            break;
//        case PostPrivacy.Private:
//            postRequest.Privacy = "Private";
//            break;
//        default:
//            postRequest.Privacy = "Private";
//            break;
//    }