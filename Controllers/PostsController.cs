using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs;
using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
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
        private readonly AppDbContext _dbcontext;
        private readonly IPostFeedService _postFeedService;

        public PostsController(AppDbContext dbcontext, IPostFeedService postFeedService)
        {
            _dbcontext = dbcontext;
            _postFeedService = postFeedService;
        }

        /// <summary>
        /// Create a new post with optional media
        /// </summary>
        [HttpPost("CreatePost")]
        public async Task<IActionResult> CreatePost([FromBody] PostMediaRequest postMedia, [FromQuery] string userId, CancellationToken cancel)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new ErrorResponse
                {
                    Message = "User ID is required",
                    ErrorCode  = ErrorCodes.VALIDATION.REQUIRED,
                    IsSuccess = false
                });
            }

            var result = await _postFeedService.CreatePostAsync(userId, postMedia, cancel);

            return Ok(new GeneralResponse<PostResponseDTO>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Post created successfully",
                IsSuccess = true,
                Data = result
            });
        }

        /// <summary>
        /// Get a single post
        /// </summary>
        [HttpGet("GetPost/{postId}")]
        public async Task<IActionResult> GetPost(long postId, [FromQuery] string? userId, CancellationToken cancel)
        {
            var result = await _postFeedService.GetPostAsync(postId, userId, cancel);

            return Ok(new GeneralResponse<PostResponseDTO>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Post retrieved successfully",
                IsSuccess = true,
                Data = result
            });
        }

        /// <summary>
        /// Get user feed
        /// </summary>
        [HttpGet("GetUserFeed")]
        public async Task<IActionResult> GetUserFeed([FromQuery] string userId, [FromQuery] SearchRequest request, CancellationToken cancel = default)
        {
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

        /// <summary>
        /// Get all posts from a user
        /// </summary>
        [HttpGet("GetUserPosts")]
        public async Task<IActionResult> GetUserPosts([FromQuery] string userId, [FromQuery] SearchRequest request, CancellationToken cancel = default)
        {
            var result = await _postFeedService.GetUserPostsAsync(userId, request, cancel);

            return Ok(new DataListResponse<PostResponseDTO>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "User posts retrieved successfully",
                IsSuccess = true,
                Data = result,
                TotalCount = result.Count
            });
        }

        /// <summary>
        /// Update a post
        /// </summary>
        [HttpPut("UpdatePost/{postId}")]
        public async Task<IActionResult> UpdatePost(long postId, [FromBody] PostMediaRequest request, [FromQuery] string userId, CancellationToken cancel)
        {
            await _postFeedService.UpdatePostAsync(postId, userId, request.Content, request.Privacy, cancel);

            return Ok(new GeneralResponse
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Post updated successfully",
                IsSuccess = true
            });
        }

        /// <summary>
        /// Delete a post
        /// </summary>
        [HttpDelete("DeletePost/{postId}")]
        public async Task<IActionResult> DeletePost(long postId, [FromQuery] string userId, CancellationToken cancel)
        {
            await _postFeedService.DeletePostAsync(postId, userId, cancel);

            return Ok(new GeneralResponse
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Post deleted successfully",
                IsSuccess = true
            });
        }

        /// <summary>
        /// Add a reaction to a post
        /// </summary>
        [HttpPost("AddReaction")]
        public async Task<IActionResult> AddReaction([FromQuery] long postId, [FromQuery] string userId, [FromQuery] byte reactionType, CancellationToken cancel)
        {
            var result = await _postFeedService.AddReactionAsync(postId, userId, reactionType, cancel);

            return Ok(new GeneralResponse<PostReactionDTO>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Reaction added successfully",
                IsSuccess = true,
                Data = result
            });
        }

        /// <summary>
        /// Remove a reaction from a post
        /// </summary>
        [HttpDelete("RemoveReaction")]
        public async Task<IActionResult> RemoveReaction([FromQuery] long postId, [FromQuery] string userId, CancellationToken cancel)
        {
            await _postFeedService.RemoveReactionAsync(postId, userId, cancel);

            return Ok(new GeneralResponse
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Reaction removed successfully",
                IsSuccess = true
            });
        }

        /// <summary>
        /// Get reactions on a post
        /// </summary>
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

        /// <summary>
        /// Add a comment to a post
        /// </summary>
        [HttpPost("AddComment")]
        public async Task<IActionResult> AddComment([FromQuery] long postId, [FromQuery] string userId, [FromBody] dynamic request, CancellationToken cancel)
        {
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

        /// <summary>
        /// Get comments on a post
        /// </summary>
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

        /// <summary>
        /// Update a comment
        /// </summary>
        [HttpPut("UpdateComment/{commentId}")]
        public async Task<IActionResult> UpdateComment(string commentId, [FromQuery] string userId, [FromBody] dynamic request, CancellationToken cancel)
        {
            string content = request.content;
            await _postFeedService.UpdateCommentAsync(commentId, userId, content, cancel);

            return Ok(new GeneralResponse
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Comment updated successfully",
                IsSuccess = true
            });
        }

        /// <summary>
        /// Delete a comment
        /// </summary>
        [HttpDelete("DeleteComment/{commentId}")]
        public async Task<IActionResult> DeleteComment(string commentId, [FromQuery] string userId, CancellationToken cancel)
        {
            await _postFeedService.DeleteCommentAsync(commentId, userId, cancel);

            return Ok(new GeneralResponse
            {
                StatusCode = StatusCodes.Status200OK,
                Message = "Comment deleted successfully",
                IsSuccess = true
            });
        }

        /// <summary>
        /// [Deprecated] Use CreatePost instead
        /// </summary>
        [HttpPost("PostFeed")]
        public async Task<IActionResult> PostFeed([FromBody] PostMediaRequest postMedia, CancellationToken cancel)
        {
            if (string.IsNullOrEmpty(postMedia.CreatedByUserId))
            {
                return BadRequest(new ErrorResponse
                {
                    Message = "User ID is required",
                    ErrorCode = ErrorCodes.VALIDATION.REQUIRED,
                    IsSuccess = false
                });
            }

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
