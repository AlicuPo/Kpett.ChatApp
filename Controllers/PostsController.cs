using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PostsController : ControllerBase
    {
        private readonly IPostFeedService _postFeedService;

        public PostsController(IPostFeedService postFeedService)
        {
            _postFeedService = postFeedService;
        }

        [HttpPost("CreatePost")]
        public async Task<IActionResult> CreatePost([FromBody] PostMediaRequest postMedia, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postFeedService.CreatePostAsync(userId, postMedia, cancel);

            return Ok(new GeneralResponse<PostResponseDTO>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Post created successfully",
                IsSuccess = true,
                Data = result
            });
        }

        [HttpGet("GetPost/{postId}")]
        public async Task<IActionResult> GetPost(long postId, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postFeedService.GetPostAsync(postId, userId, cancel);

            return Ok(new GeneralResponse<PostResponseDTO>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Post retrieved successfully",
                IsSuccess = true,
                Data = result
            });
        }

        [HttpGet("GetUserFeed")]
        public async Task<IActionResult> GetUserFeed([FromQuery] SearchRequest request, CancellationToken cancel = default)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postFeedService.GetUserFeedAsync(userId, request, cancel);

            return Ok(new DataListResponse<UserFeedDTO>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "User feed retrieved successfully",
                IsSuccess = true,
                Data = result,
                TotalCount = result.Count
            });
        }

        [HttpGet("GetUserPosts")]
        public async Task<IActionResult> GetUserPosts([FromQuery] string? userId, [FromQuery] SearchRequest request, CancellationToken cancel = default)
        {
            var targetUserId = string.IsNullOrWhiteSpace(userId) ? User.GetRequiredUserId() : userId;
            var result = await _postFeedService.GetUserPostsAsync(targetUserId, request, cancel);

            return Ok(new DataListResponse<PostResponseDTO>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "User posts retrieved successfully",
                IsSuccess = true,
                Data = result,
                TotalCount = result.Count
            });
        }

        [HttpPut("UpdatePost/{postId}")]
        public async Task<IActionResult> UpdatePost(long postId, [FromBody] PostMediaRequest request, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            await _postFeedService.UpdatePostAsync(postId, userId, request.Content ?? string.Empty, request.Privacy ?? string.Empty, cancel);

            return Ok(new GeneralResponse
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Post updated successfully",
                IsSuccess = true
            });
        }

        [HttpDelete("DeletePost/{postId}")]
        public async Task<IActionResult> DeletePost(long postId, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            await _postFeedService.DeletePostAsync(postId, userId, cancel);

            return Ok(new GeneralResponse
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Post deleted successfully",
                IsSuccess = true
            });
        }

        [HttpPost("AddReaction")]
        public async Task<IActionResult> AddReaction([FromQuery] long postId, [FromQuery] byte reactionType, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postFeedService.AddReactionAsync(postId, userId, reactionType, cancel);

            return Ok(new GeneralResponse<PostReactionDTO>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Reaction added successfully",
                IsSuccess = true,
                Data = result
            });
        }

        [HttpDelete("RemoveReaction")]
        public async Task<IActionResult> RemoveReaction([FromQuery] long postId, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            await _postFeedService.RemoveReactionAsync(postId, userId, cancel);

            return Ok(new GeneralResponse
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Reaction removed successfully",
                IsSuccess = true
            });
        }

        [HttpGet("GetReactions/{postId}")]
        public async Task<IActionResult> GetReactions(long postId, CancellationToken cancel)
        {
            var result = await _postFeedService.GetPostReactionsAsync(postId, cancel);

            return Ok(new DataListResponse<PostReactionDTO>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Post reactions retrieved successfully",
                IsSuccess = true,
                Data = result,
                TotalCount = result.Count
            });
        }

        [HttpPost("AddComment")]
        public async Task<IActionResult> AddComment([FromQuery] long postId, [FromBody] dynamic request, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            string content = request.content;
            string? parentCommentId = request.parentCommentId;

            var result = await _postFeedService.AddCommentAsync(postId, userId, content, parentCommentId, cancel);

            return Ok(new GeneralResponse<CommentDTO>
            {
                StatusCode = StatusCodes.Status201Created,
                Message = "Comment added successfully",
                IsSuccess = true,
                Data = result
            });
        }

        [HttpGet("GetComments/{postId}")]
        public async Task<IActionResult> GetComments(long postId, CancellationToken cancel)
        {
            var result = await _postFeedService.GetCommentsAsync(postId, cancel);

            return Ok(new DataListResponse<CommentDTO>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Post comments retrieved successfully",
                IsSuccess = true,
                Data = result,
                TotalCount = result.Count
            });
        }

        [HttpPut("UpdateComment/{commentId}")]
        public async Task<IActionResult> UpdateComment(string commentId, [FromBody] dynamic request, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            string content = request.content;
            await _postFeedService.UpdateCommentAsync(commentId, userId, content, cancel);

            return Ok(new GeneralResponse
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Comment updated successfully",
                IsSuccess = true
            });
        }

        [HttpDelete("DeleteComment/{commentId}")]
        public async Task<IActionResult> DeleteComment(string commentId, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            await _postFeedService.DeleteCommentAsync(commentId, userId, cancel);

            return Ok(new GeneralResponse
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Comment deleted successfully",
                IsSuccess = true
            });
        }

        [HttpPost("PostFeed")]
        public async Task<IActionResult> PostFeed([FromBody] PostMediaRequest postMedia, CancellationToken cancel)
        {
            postMedia.CreatedByUserId = User.GetRequiredUserId();
            await _postFeedService.PostFeed(postMedia, cancel);

            return Ok(new GeneralResponse
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Post created successfully",
                IsSuccess = true
            });
        }
    }
}
