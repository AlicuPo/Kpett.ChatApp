using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/comments")]
    [ApiController]
    [Authorize]
    public class CommentsController : ControllerBase
    {
        private readonly IPostService _postFeedService;

        public CommentsController(IPostService postFeedService)
        {
            _postFeedService = postFeedService;
        }

        [HttpPatch("{commentId}")]
        public async Task<ActionResult<CommentDTO>> UpdateComment(string commentId, [FromBody] UpdateCommentRequest request, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postFeedService.UpdateCommentAsync(commentId, userId, request?.Content ?? string.Empty, cancel);
            return Ok(result);
        }

        [HttpDelete("{commentId}")]
        public async Task<IActionResult> DeleteComment(string commentId, CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            await _postFeedService.DeleteCommentAsync(commentId, userId, cancel);
            return NoContent();
        }
    }
}
