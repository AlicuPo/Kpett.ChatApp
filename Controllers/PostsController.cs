using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Filters;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;

        public PostsController(IPostService postFeedService)
        {
            _postService = postFeedService;
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult> CreatePost([FromBody] PostRequest postRequest, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postService.CreatePostAsync(userId, postRequest, cancel);
            return Ok(new GeneralResponse<PostFeedResponse>
            {
                IsSuccess = true,
                Message = "Create post successfully",
                Data = result,
                StatusCode = 201
            });
        }

        [HttpPut("{postId}")]
        [Authorize]
        public async Task<ActionResult> UpdatePost(string postId, [FromBody] PostRequest postRequest, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postService.UpdatePostAsync(postId, userId, postRequest, cancel);
            return Ok(new GeneralResponse<PostFeedResponse>
            {
                IsSuccess = true,
                Message = "Create post successfully",
                Data = result,
                StatusCode = 200
            });
        }

        [HttpGet]
        [OptionalAuthorize]
        public async Task<ActionResult<GeneralResponse<PaginatedData<PostFeedResponse>>>> GetPostFeed(
            [FromQuery] string? cursor = null,
            [FromQuery] int limit = 10,
            CancellationToken cancel = default)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var result = await _postService.GetFeedAsync(currentUserId, cursor, limit, cancel);

            return Ok(new GeneralResponse<PaginatedData<PostFeedResponse>>
            {
                IsSuccess = true,
                Message = "Get posts feed successfully",
                Data = result,
                StatusCode = 200
            });
        }

        [HttpGet("users/{userId}")]
        [OptionalAuthorize]
        public async Task<ActionResult> GetUserPosts(
            string userId,
            [FromQuery] SearchRequest searchRequest,
            [FromQuery] CursorPaginationRequest cursorPagination,
            CancellationToken cancel = default)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var result = await _postService.GetPostsByUserIdAsync(userId, currentUserId, searchRequest, cursorPagination, cancel);

            return Ok(new GeneralResponse<PaginatedData<PostThumbnailResponse>>
            {
                IsSuccess = true,
                Message = "Get post thumbnail successfully",
                Data = result,
                StatusCode = 200
            });
        }

        [HttpGet("{postId}")]
        [OptionalAuthorize]
        public async Task<ActionResult> GetPost(string postId, CancellationToken cancel)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _postService.GetPostByIdAsync(postId, currentUserId, cancel);
            return Ok(new GeneralResponse<PostFeedResponse>
            {
                IsSuccess = true,
                Message = "Get post by id successfully",
                Data = result,
                StatusCode = StatusCodes.Status200OK
            });
        }

        [HttpDelete("{postId}")]
        [Authorize]
        public async Task<IActionResult> DeletePost(string postId, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            await _postService.DeletePostAsync(postId, userId, cancel);
            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Delete post successfully"
            });
        }

        [HttpPut("{postId}/reactions")]
        [Authorize]
        public async Task<ActionResult<GeneralResponse<PostReactionDTO>>> UpsertReaction(string postId, [FromBody] UpsertReactionRequest request, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postService.AddReactionAsync(postId, userId, request?.ReactionType ?? 0, cancel);
            return Ok(new GeneralResponse<PostReactionDTO>
            {
                IsSuccess = true,
                Message = "Add reaction successfully",
                StatusCode = StatusCodes.Status200OK,
                Data = result
            });
        }

        [HttpDelete("{postId}/reactions")]
        [Authorize]
        public async Task<IActionResult> RemoveReaction(string postId, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            await _postService.RemoveReactionAsync(postId, userId, cancel);
            return NoContent();
        }

        [HttpGet("{postId}/reactions")]
        public async Task<ActionResult> GetReactions(string postId, CancellationToken cancel)
        {
            var result = await _postService.GetPostReactionsAsync(postId, cancel);
            return Ok(result);
        }
    }
}
