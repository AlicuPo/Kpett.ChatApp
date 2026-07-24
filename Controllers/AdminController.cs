using Kpett.ChatApp.Constants;
using Kpett.ChatApp.Data;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Controllers;

[Route("api/admin")]
[Authorize(Roles = "SuperAdmin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AdminController> _logger;

    public AdminController(AppDbContext dbContext, ILogger<AdminController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpDelete("posts/{postId}")]
    public async Task<IActionResult> DeletePost(string postId, CancellationToken cancel)
    {
        var post = await _dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancel);
        if (post == null)
            throw new NotFoundException(ErrorCodes.POST.NOT_FOUND, "Post not found");

        post.IsDeleted = true;
        post.UpdatedAt = DateTime.UtcNow;

        _dbContext.Posts.Update(post);
        await _dbContext.SaveChangesAsync(cancel);

        _logger.LogInformation("SuperAdmin deleted post {PostId}", postId);

        return Ok(new GeneralResponse
        {
            IsSuccess = true,
            StatusCode = 200,
            Message = "Post deleted successfully."
        });
    }

    [HttpDelete("users/{userId}")]
    public async Task<IActionResult> DeleteUser(string userId, CancellationToken cancel)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancel);
        if (user == null)
            throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found");

        user.IsActive = false;

        await _dbContext.SaveChangesAsync(cancel);

        _logger.LogInformation("SuperAdmin deactivated user {UserId}", userId);

        return Ok(new GeneralResponse
        {
            IsSuccess = true,
            StatusCode = 200,
            Message = "User deactivated successfully."
        });
    }
}
