using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Services.Impls;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommentsController : ControllerBase
    {
        private readonly ICommentService _commentService;

        public CommentsController(ICommentService commentService)
        {
            _commentService = commentService;
        }

        [HttpPost("posts/{postId}")]
        [Authorize]
        public async Task<ActionResult<GeneralResponse<CommentListItemDTO>>> AddComment(string postId, [FromBody] CreateCommentRequest request, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _commentService.AddCommentAsync(
                postId,
                userId,
                request?.Content ?? string.Empty,
                request?.ParentCommentId,
                cancel);
            return Created($"/api/comments/{result.Id}", new GeneralResponse<CommentListItemDTO>
            {
                IsSuccess = true,
                Message = "Get comment successfully",
                Data = result,
                StatusCode = 201
            });
        }

        [HttpGet("posts/{postId}")]
        public async Task<ActionResult<GeneralResponse<PaginatedData<CommentListItemDTO>>>> GetComments(
            string postId,
            [FromQuery] string parentCommentId,
            [FromQuery] string cursor,
            [FromQuery] int limit = 10,
            CancellationToken cancel = default)
        {
            var userId = User.GetRequiredUserId();
            var result = await _commentService.GetCommentsAsync(postId, parentCommentId, userId, cursor, limit, cancel);
            return Ok(new GeneralResponse<PaginatedData<CommentListItemDTO>>
            {
                IsSuccess = true,
                Message = "Get comments successfully",
                StatusCode = 200,
                Data = result
            });
        }

        [HttpPut("posts/{commentId}")]
        [Authorize]
        public async Task<ActionResult<CommentDTO>> UpdateComment(string commentId, [FromBody] UpdateCommentRequest request, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _commentService.UpdateCommentAsync(commentId, userId, request?.Content ?? string.Empty, cancel);
            return Ok(new GeneralResponse<CommentListItemDTO>
            {
                IsSuccess = true,
                Data = result,
                Message = "Update comment successfully",
                StatusCode = StatusCodes.Status200OK
            });
        }

        [HttpDelete("posts/{commentId}")]
        [Authorize]
        public async Task<IActionResult> DeleteComment(string commentId, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            await _commentService.DeleteCommentAsync(commentId, userId, cancel);
            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Comment deleted successfully",
                StatusCode = 200
            });
        }
    }
}
