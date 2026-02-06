using Kpett.ChatApp.DTOs;
using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PostController : ControllerBase
    {
        private readonly AppDbContext _dbcontext;
        private readonly IPostFeedService _postFeedService;

        public PostController(AppDbContext dbcontext, IPostFeedService postFeedService)
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
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new GeneralResponse
                    {
                        Message = "User ID is required",
                        ErorrCode = StatusCodes.Status400BadRequest,
                        Return = false
                    });
                }

                var result = await _postFeedService.CreatePostAsync(userId, postMedia, cancel);

                return Ok(new GeneralResponse<PostResponseDTO>
                {
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Post created successfully",
                    Return = true,
                    Data = result
                });
            }
            catch (AppException appEx)
            {
                return StatusCode(appEx.StatusCode, new GeneralResponse
                {
                    Message = appEx.Message,
                    ErorrCode = appEx.StatusCode,
                    Return = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new GeneralResponse
                {
                    Message = $"An error occurred: {ex.Message}",
                    ErorrCode = StatusCodes.Status500InternalServerError,
                    Return = false
                });
            }
        }

        /// <summary>
        /// Get a single post
        /// </summary>
        [HttpGet("GetPost/{postId}")]
        public async Task<IActionResult> GetPost(long postId, [FromQuery] string? userId, CancellationToken cancel)
        {
            try
            {
                var result = await _postFeedService.GetPostAsync(postId, userId, cancel);

                return Ok(new GeneralResponse<PostResponseDTO>
                {
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Post retrieved successfully",
                    Return = true,
                    Data = result
                });
            }
            catch (AppException appEx)
            {
                return StatusCode(appEx.StatusCode, new GeneralResponse
                {
                    Message = appEx.Message,
                    ErorrCode = appEx.StatusCode,
                    Return = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new GeneralResponse
                {
                    Message = $"An error occurred: {ex.Message}",
                    ErorrCode = StatusCodes.Status500InternalServerError,
                    Return = false
                });
            }
        }

        /// <summary>
        /// Get user feed
        /// </summary>
        [HttpGet("GetUserFeed")]
        public async Task<IActionResult> GetUserFeed([FromQuery] string userId, [FromQuery] SearchRequest request, CancellationToken cancel = default)
        {
            try
            {
                var result = await _postFeedService.GetUserFeedAsync(userId, request, cancel);

                return Ok(new DataListResponse<UserFeedDTO>
                {
                    StatusCode = StatusCodes.Status200OK,
                    Message = "User feed retrieved successfully",
                    Return = true,
                    Data = result,
                    TotalCount = result.Count
                });
            }
            catch (AppException appEx)
            {
                return StatusCode(appEx.StatusCode, new GeneralResponse
                {
                    Message = appEx.Message,
                    ErorrCode = appEx.StatusCode,
                    Return = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new GeneralResponse
                {
                    Message = $"An error occurred: {ex.Message}",
                    ErorrCode = StatusCodes.Status500InternalServerError,
                    Return = false
                });
            }
        }

        /// <summary>
        /// Get all posts from a user
        /// </summary>
        [HttpGet("GetUserPosts")]
        public async Task<IActionResult> GetUserPosts([FromQuery] string userId, [FromQuery] SearchRequest request, CancellationToken cancel = default)
        {
            try
            {
                var result = await _postFeedService.GetUserPostsAsync(userId, request, cancel);

                return Ok(new DataListResponse<PostResponseDTO>
                {
                    StatusCode = StatusCodes.Status200OK,
                    Message = "User posts retrieved successfully",
                    Return = true,
                    Data = result,
                    TotalCount = result.Count
                });
            }
            catch (AppException appEx)
            {
                return StatusCode(appEx.StatusCode, new GeneralResponse
                {
                    Message = appEx.Message,
                    ErorrCode = appEx.StatusCode,
                    Return = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new GeneralResponse
                {
                    Message = $"An error occurred: {ex.Message}",
                    ErorrCode = StatusCodes.Status500InternalServerError,
                    Return = false
                });
            }
        }

        /// <summary>
        /// Update a post
        /// </summary>
        [HttpPut("UpdatePost/{postId}")]
        public async Task<IActionResult> UpdatePost(long postId, [FromBody] PostMediaRequest request, [FromQuery] string userId, CancellationToken cancel)
        {
            try
            {
                await _postFeedService.UpdatePostAsync(postId, userId, request.Content, request.Privacy, cancel);

                return Ok(new GeneralResponse
                {
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Post updated successfully",
                    Return = true
                });
            }
            catch (AppException appEx)
            {
                return StatusCode(appEx.StatusCode, new GeneralResponse
                {
                    Message = appEx.Message,
                    ErorrCode = appEx.StatusCode,
                    Return = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new GeneralResponse
                {
                    Message = $"An error occurred: {ex.Message}",
                    ErorrCode = StatusCodes.Status500InternalServerError,
                    Return = false
                });
            }
        }

        /// <summary>
        /// Delete a post
        /// </summary>
        [HttpDelete("DeletePost/{postId}")]
        public async Task<IActionResult> DeletePost(long postId, [FromQuery] string userId, CancellationToken cancel)
        {
            try
            {
                await _postFeedService.DeletePostAsync(postId, userId, cancel);

                return Ok(new GeneralResponse
                {
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Post deleted successfully",
                    Return = true
                });
            }
            catch (AppException appEx)
            {
                return StatusCode(appEx.StatusCode, new GeneralResponse
                {
                    Message = appEx.Message,
                    ErorrCode = appEx.StatusCode,
                    Return = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new GeneralResponse
                {
                    Message = $"An error occurred: {ex.Message}",
                    ErorrCode = StatusCodes.Status500InternalServerError,
                    Return = false
                });
            }
        }

        /// <summary>
        /// Add a reaction to a post
        /// </summary>
        [HttpPost("AddReaction")]
        public async Task<IActionResult> AddReaction([FromQuery] long postId, [FromQuery] string userId, [FromQuery] byte reactionType, CancellationToken cancel)
        {
            try
            {
                var result = await _postFeedService.AddReactionAsync(postId, userId, reactionType, cancel);

                return Ok(new GeneralResponse<PostReactionDTO>
                {
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Reaction added successfully",
                    Return = true,
                    Data = result
                });
            }
            catch (AppException appEx)
            {
                return StatusCode(appEx.StatusCode, new GeneralResponse
                {
                    Message = appEx.Message,
                    ErorrCode = appEx.StatusCode,
                    Return = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new GeneralResponse
                {
                    Message = $"An error occurred: {ex.Message}",
                    ErorrCode = StatusCodes.Status500InternalServerError,
                    Return = false
                });
            }
        }

        /// <summary>
        /// Remove a reaction from a post
        /// </summary>
        [HttpDelete("RemoveReaction")]
        public async Task<IActionResult> RemoveReaction([FromQuery] long postId, [FromQuery] string userId, CancellationToken cancel)
        {
            try
            {
                await _postFeedService.RemoveReactionAsync(postId, userId, cancel);

                return Ok(new GeneralResponse
                {
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Reaction removed successfully",
                    Return = true
                });
            }
            catch (AppException appEx)
            {
                return StatusCode(appEx.StatusCode, new GeneralResponse
                {
                    Message = appEx.Message,
                    ErorrCode = appEx.StatusCode,
                    Return = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new GeneralResponse
                {
                    Message = $"An error occurred: {ex.Message}",
                    ErorrCode = StatusCodes.Status500InternalServerError,
                    Return = false
                });
            }
        }

        /// <summary>
        /// Get reactions on a post
        /// </summary>
        [HttpGet("GetReactions/{postId}")]
        public async Task<IActionResult> GetReactions(long postId, CancellationToken cancel)
        {
            try
            {
                var result = await _postFeedService.GetPostReactionsAsync(postId, cancel);

                return Ok(new DataListResponse<PostReactionDTO>
                {
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Post reactions retrieved successfully",
                    Return = true,
                    Data = result,
                    TotalCount = result.Count
                });
            }
            catch (AppException appEx)
            {
                return StatusCode(appEx.StatusCode, new GeneralResponse
                {
                    Message = appEx.Message,
                    ErorrCode = appEx.StatusCode,
                    Return = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new GeneralResponse
                {
                    Message = $"An error occurred: {ex.Message}",
                    ErorrCode = StatusCodes.Status500InternalServerError,
                    Return = false
                });
            }
        }

        /// <summary>
        /// Add a comment to a post
        /// </summary>
        [HttpPost("AddComment")]
        public async Task<IActionResult> AddComment([FromQuery] long postId, [FromQuery] string userId, [FromBody] dynamic request, CancellationToken cancel)
        {
            try
            {
                string content = request.content;
                string? parentCommentId = request.parentCommentId;

                var result = await _postFeedService.AddCommentAsync(postId, userId, content, parentCommentId, cancel);

                return Ok(new GeneralResponse<CommentDTO>
                {
                    StatusCode = StatusCodes.Status201Created,
                    Message = "Comment added successfully",
                    Return = true,
                    Data = result
                });
            }
            catch (AppException appEx)
            {
                return StatusCode(appEx.StatusCode, new GeneralResponse
                {
                    Message = appEx.Message,
                    ErorrCode = appEx.StatusCode,
                    Return = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new GeneralResponse
                {
                    Message = $"An error occurred: {ex.Message}",
                    ErorrCode = StatusCodes.Status500InternalServerError,
                    Return = false
                });
            }
        }

        /// <summary>
        /// Get comments on a post
        /// </summary>
        [HttpGet("GetComments/{postId}")]
        public async Task<IActionResult> GetComments(long postId, CancellationToken cancel)
        {
            try
            {
                var result = await _postFeedService.GetCommentsAsync(postId, cancel);

                return Ok(new DataListResponse<CommentDTO>
                {
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Post comments retrieved successfully",
                    Return = true,
                    Data = result,
                    TotalCount = result.Count
                });
            }
            catch (AppException appEx)
            {
                return StatusCode(appEx.StatusCode, new GeneralResponse
                {
                    Message = appEx.Message,
                    ErorrCode = appEx.StatusCode,
                    Return = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new GeneralResponse
                {
                    Message = $"An error occurred: {ex.Message}",
                    ErorrCode = StatusCodes.Status500InternalServerError,
                    Return = false
                });
            }
        }

        /// <summary>
        /// Update a comment
        /// </summary>
        [HttpPut("UpdateComment/{commentId}")]
        public async Task<IActionResult> UpdateComment(string commentId, [FromQuery] string userId, [FromBody] dynamic request, CancellationToken cancel)
        {
            try
            {
                string content = request.content;
                await _postFeedService.UpdateCommentAsync(commentId, userId, content, cancel);

                return Ok(new GeneralResponse
                {
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Comment updated successfully",
                    Return = true
                });
            }
            catch (AppException appEx)
            {
                return StatusCode(appEx.StatusCode, new GeneralResponse
                {
                    Message = appEx.Message,
                    ErorrCode = appEx.StatusCode,
                    Return = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new GeneralResponse
                {
                    Message = $"An error occurred: {ex.Message}",
                    ErorrCode = StatusCodes.Status500InternalServerError,
                    Return = false
                });
            }
        }

        /// <summary>
        /// Delete a comment
        /// </summary>
        [HttpDelete("DeleteComment/{commentId}")]
        public async Task<IActionResult> DeleteComment(string commentId, [FromQuery] string userId, CancellationToken cancel)
        {
            try
            {
                await _postFeedService.DeleteCommentAsync(commentId, userId, cancel);

                return Ok(new GeneralResponse
                {
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Comment deleted successfully",
                    Return = true
                });
            }
            catch (AppException appEx)
            {
                return StatusCode(appEx.StatusCode, new GeneralResponse
                {
                    Message = appEx.Message,
                    ErorrCode = appEx.StatusCode,
                    Return = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new GeneralResponse
                {
                    Message = $"An error occurred: {ex.Message}",
                    ErorrCode = StatusCodes.Status500InternalServerError,
                    Return = false
                });
            }
        }

        /// <summary>
        /// [Deprecated] Use CreatePost instead
        /// </summary>
        [HttpPost("PostFeed")]
        public async Task<IActionResult> PostFeed([FromBody] PostMediaRequest postMedia, CancellationToken cancel)
        {
            try
            {
                if (string.IsNullOrEmpty(postMedia.CreatedByUserId))
                {
                    return BadRequest(new GeneralResponse
                    {
                        Message = "User ID is required",
                        ErorrCode = StatusCodes.Status400BadRequest,
                        Return = false
                    });
                }

                await _postFeedService.PostFeed(postMedia, cancel);
                return Ok(new GeneralResponse
                {
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Post created successfully",
                    Return = true
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new GeneralResponse
                {
                    Message = ex.Message,
                    ErorrCode = StatusCodes.Status400BadRequest,
                    Return = false
                });
            }
        }
    }
}
