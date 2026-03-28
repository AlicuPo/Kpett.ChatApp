using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/posts")]
    [ApiController]
    [Authorize]
    public class PostsController : ControllerBase
    {
        private readonly IPostFeedService _postFeedService;

        public PostsController(IPostFeedService postFeedService)
        {
            _postFeedService = postFeedService;
        }

        [HttpPost]
        public async Task<ActionResult<PostResponseDTO>> CreatePost([FromBody] PostMediaRequest postMedia, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postFeedService.CreatePostAsync(userId, postMedia, cancel);
            return CreatedAtAction(nameof(GetPost), new { postId = result.Id }, result);
        }

        [HttpGet("{postId:long}")]
        public async Task<ActionResult<PostResponseDTO>> GetPost(long postId, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postFeedService.GetPostAsync(postId, userId, cancel);
            return Ok(result);
        }

        [HttpPatch("{postId:long}")]
        public async Task<ActionResult<PostResponseDTO>> UpdatePost(long postId, [FromBody] PostMediaRequest request, CancellationToken cancel)
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

        [HttpDelete("{postId:long}")]
        public async Task<IActionResult> DeletePost(long postId, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            await _postFeedService.DeletePostAsync(postId, userId, cancel);
            return NoContent();
        }

        [HttpPut("{postId:long}/reactions/me")]
        public async Task<ActionResult<PostReactionDTO>> UpsertReaction(long postId, [FromBody] UpsertReactionRequest request, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postFeedService.AddReactionAsync(postId, userId, request?.ReactionType ?? 0, cancel);
            return Ok(result);
        }

        [HttpDelete("{postId:long}/reactions/me")]
        public async Task<IActionResult> RemoveReaction(long postId, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            await _postFeedService.RemoveReactionAsync(postId, userId, cancel);
            return NoContent();
        }

        [HttpGet("{postId:long}/reactions")]
        public async Task<ActionResult<List<PostReactionDTO>>> GetReactions(long postId, CancellationToken cancel)
        {
            var result = await _postFeedService.GetPostReactionsAsync(postId, cancel);
            return Ok(result);
        }

        [HttpPost("{postId:long}/comments")]
        public async Task<ActionResult<CommentDTO>> AddComment(long postId, [FromBody] CreateCommentRequest request, CancellationToken cancel)
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

        [HttpGet("{postId:long}/comments")]
        public async Task<ActionResult<List<CommentDTO>>> GetComments(long postId, CancellationToken cancel)
        {
            var result = await _postFeedService.GetCommentsAsync(postId, cancel);
            return Ok(result);
        }
    }
}
