using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Helpers;
using Kpett.ChatApp.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers;

[Route("api/[controller]")]
[Authorize]
public class SavedPostsController : ControllerBase
{
    private readonly ISavedPostService _savedPostService;

    public SavedPostsController(ISavedPostService savedPostService)
    {
        _savedPostService = savedPostService;
    }

    [HttpPost("{postId}")]
    public async Task<ActionResult> SavePost(string postId, CancellationToken cancel)
    {
        var userId = User.GetRequiredUserId();
        await _savedPostService.SavePostAsync(userId, postId, cancel);
        return Ok(new GeneralResponse
        {
            IsSuccess = true,
            Message = "Post saved successfully",
            StatusCode = 200
        });
    }

    [HttpDelete("{postId}")]
    public async Task<ActionResult> UnsavePost(string postId, CancellationToken cancel)
    {
        var userId = User.GetRequiredUserId();
        await _savedPostService.UnsavePostAsync(userId, postId, cancel);
        return Ok(new GeneralResponse
        {
            IsSuccess = true,
            Message = "Post unsaved successfully",
            StatusCode = 200
        });
    }

    [HttpGet("{postId}/check")]
    public async Task<ActionResult<GeneralResponse<bool>>> CheckSaved(string postId, CancellationToken cancel)
    {
        var userId = User.GetRequiredUserId();
        var isSaved = await _savedPostService.IsPostSavedAsync(userId, postId, cancel);
        return Ok(new GeneralResponse<bool>
        {
            IsSuccess = true,
            Data = isSaved,
            StatusCode = 200
        });
    }

    [HttpGet]
    public async Task<ActionResult<GeneralResponse<PaginatedData<PostThumbnailResponse>>>> GetSavedPosts(
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 20,
        CancellationToken cancel = default)
    {
        var userId = User.GetRequiredUserId();
        var result = await _savedPostService.GetSavedPostsAsync(userId, userId, cursor, limit, cancel);
        return Ok(new GeneralResponse<PaginatedData<PostThumbnailResponse>>
        {
            IsSuccess = true,
            Message = "Get saved posts successfully",
            Data = result,
            StatusCode = 200
        });
    }
}
