using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly AppDbContext _context;

        public PostsController(IPostService postFeedService, AppDbContext context)
        {
            _postService = postFeedService;
            _context = context;
        }

        [HttpPost]
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
        public async Task<ActionResult> GetPostFeed(
            [FromQuery] string? cursor = null,
            [FromQuery] int limit = 10,
            CancellationToken cancel = default)
        {
            var userId = User.GetRequiredUserId();

            var result = await _postService.GetFeedAsync(userId, cursor, limit, cancel);

            return Ok(new GeneralResponse<PaginatedData<PostFeedResponse>>
            {
                IsSuccess = true,
                Message = "Get posts feed successfully",
                Data = result,
                StatusCode = 200
            });
        }

        [HttpGet("users/{userId}")]
        public async Task<ActionResult> GetUserPosts(
            string userId,
            [FromQuery] SearchRequest searchRequest,
            [FromQuery] CursorPaginationRequest cursorPagination,
            CancellationToken cancel = default)
        {
            var currentUserId = User.GetRequiredUserId();

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
        public async Task<ActionResult> GetPost(string postId, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postService.GetPostByIdAsync(postId, userId, cancel);
            return Ok(new GeneralResponse<PostFeedResponse>
            {
                IsSuccess = true,
                Message = "Get post by id successfully",
                Data = result,
                StatusCode = StatusCodes.Status200OK
            });
        }

        [HttpDelete("{postId}")]
        public async Task<IActionResult> DeletePost(string postId, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            await _postService.DeletePostAsync(postId, userId, cancel);
            return NoContent();
        }

        [HttpDelete("media/{publicId}")]
        public async Task<IActionResult> DeleteMedia(string publicId, [FromQuery] string resourceType)
        {
            await _postService.DeleteMedia(publicId, resourceType);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Delete media successfully",
                StatusCode = StatusCodes.Status200OK
            });
        }

        [HttpPut("{postId}/reactions/me")]
        public async Task<ActionResult> UpsertReaction(string postId, [FromBody] UpsertReactionRequest request, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postService.AddReactionAsync(postId, userId, request?.ReactionType ?? 0, cancel);
            return Ok(result);
        }

        [HttpDelete("{postId}/reactions/me")]
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
