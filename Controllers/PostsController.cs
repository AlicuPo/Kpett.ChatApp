using Kpett.ChatApp.DTOs.Request.Post;
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
        private readonly IPostService _postFeedService;
        private readonly AppDbContext _context;

        public PostsController(IPostService postFeedService, AppDbContext context)
        {
            _postFeedService = postFeedService;
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<PostResponseDTO>> CreatePost([FromBody] PostRequest postRequest, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postFeedService.CreatePostAsync(userId, postRequest, cancel);
            return Ok(new GeneralResponse<string>
            {
                IsSuccess = true,
                Message = "Create post successfully",
                Data = result,
                StatusCode = 201
            });
        }

        [HttpGet]
        public async Task<ActionResult<GeneralResponse<PaginatedData<PostFeedResponse>>>> GetPostFeed(
            [FromQuery] string? cursor = null,
            [FromQuery] int limit = 10,
            CancellationToken cancel = default)
        {
            var userId = User.GetRequiredUserId();

            var result = await _postFeedService.GetFeedAsync(userId, cursor, limit, cancel);

            return Ok(new GeneralResponse<PaginatedData<PostFeedResponse>>
            {
                IsSuccess = true,
                Message = "Lấy danh sách bài viết thành công",
                Data = result,
                StatusCode = 200
            });
        }

        [HttpGet("{postId}")]
        public async Task<ActionResult<PostResponseDTO>> GetPost(string postId, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postFeedService.GetPostByIdAsync(postId, userId, cancel);
            return Ok(result);
        }

        [HttpPatch("{postId}")]
        public async Task<ActionResult<PostResponseDTO>> UpdatePost(string postId, [FromBody] PostRequest request, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postFeedService.UpdatePostAsync(
                postId,
                userId,
                request?.Content ?? string.Empty,
                request?.Privacy ?? string.Empty,
                cancel);

            return Ok(result);
        }

        [HttpDelete("{postId}")]
        public async Task<IActionResult> DeletePost(string postId, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            await _postFeedService.DeletePostAsync(postId, userId, cancel);
            return NoContent();
        }

        [HttpDelete("media/{publicId}")]
        public async Task<IActionResult> DeleteMedia(string publicId, [FromQuery] string resourceType)
        {
            await _postFeedService.DeleteMedia(publicId, resourceType);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Delete media successfully",
                StatusCode = StatusCodes.Status200OK
            });
        }

        [HttpPut("{postId}/reactions/me")]
        public async Task<ActionResult<PostReactionDTO>> UpsertReaction(string postId, [FromBody] UpsertReactionRequest request, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postFeedService.AddReactionAsync(postId, userId, request?.ReactionType ?? 0, cancel);
            return Ok(result);
        }

        [HttpDelete("{postId}/reactions/me")]
        public async Task<IActionResult> RemoveReaction(string postId, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            await _postFeedService.RemoveReactionAsync(postId, userId, cancel);
            return NoContent();
        }

        [HttpGet("{postId}/reactions")]
        public async Task<ActionResult<List<PostReactionDTO>>> GetReactions(string postId, CancellationToken cancel)
        {
            var result = await _postFeedService.GetPostReactionsAsync(postId, cancel);
            return Ok(result);
        }

        [HttpPost("{postId}/comments")]
        public async Task<ActionResult<CommentDTO>> AddComment(string postId, [FromBody] CreateCommentRequest request, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postFeedService.AddCommentAsync(
                postId,
                userId,
                request?.Content ?? string.Empty,
                request?.ParentCommentId,
                request?.Mentions,
                cancel);
            return Created($"/api/comments/{result.Id}", result);
        }

        [HttpGet("{postId}/comments")]
        public async Task<ActionResult<GeneralResponse<CommentsPageDTO>>> GetComments(
            string postId,
            [FromQuery] DateTime? cursor,
            [FromQuery] int limit = 20,
            CancellationToken cancel = default)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postFeedService.GetCommentsAsync(postId, userId, cursor, limit, cancel);
            return Ok(new GeneralResponse<CommentsPageDTO>
            {
                IsSuccess = true,
                Message = "Get comments successfully",
                StatusCode = 200,
                Data = result
            });
        }
    }
}
