using Kpett.ChatApp.DTOs.Response.Media;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class MediaController : Controller
    {
        private readonly IMediaService _mediaService;

        public MediaController(IMediaService mediaService)
        {
            _mediaService = mediaService;
        }

        [HttpPost("upload")]
        [RequestSizeLimit(200 * 1024 * 1024)]
        public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string? folder = null)
        {
            var subDir = folder ?? (file.ContentType.StartsWith("video/") ? "videos" : "images");
            var result = await _mediaService.UploadAsync(file, subDir);
            result.SecureUrl = BuildAbsoluteUrl(result.SecureUrl);
            return Ok(new GeneralResponse<MediaUploadResponse>
            {
                IsSuccess = true,
                Message = "Upload thành công",
                Data = result,
                StatusCode = 200
            });
        }

        [HttpPost("upload-image")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> UploadImage(IFormFile file, [FromQuery] string folder = "images")
        {
            var result = await _mediaService.UploadAsync(file, folder);
            result.SecureUrl = BuildAbsoluteUrl(result.SecureUrl);
            return Ok(new GeneralResponse<MediaUploadResponse>
            {
                IsSuccess = true,
                Message = "Upload image thành công",
                Data = result,
                StatusCode = 200
            });
        }

        [HttpPost("upload-video")]
        [RequestSizeLimit(200 * 1024 * 1024)]
        public async Task<IActionResult> UploadVideo(IFormFile file, [FromQuery] string folder = "videos")
        {
            var result = await _mediaService.UploadAsync(file, folder);
            result.SecureUrl = BuildAbsoluteUrl(result.SecureUrl);
            return Ok(new GeneralResponse<MediaUploadResponse>
            {
                IsSuccess = true,
                Message = "Upload video thành công",
                Data = result,
                StatusCode = 200
            });
        }

        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteMedia([FromQuery] string fileUrl)
        {
            var isDeleted = await _mediaService.DeleteAsync(fileUrl);
            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = isDeleted ? "File deleted successfully" : "File not found"
            });
        }

        private string BuildAbsoluteUrl(string relativeUrl)
        {
            var request = HttpContext.Request;
            return $"{request.Scheme}://{request.Host}{relativeUrl}";
        }
    }
}
