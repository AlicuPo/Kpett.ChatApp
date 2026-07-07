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

        [HttpGet("generate-signature")]
        public async Task<IActionResult> GetUploadSignature([FromQuery] string folder = "posts")
        {
            var result = await _mediaService.GenerateUploadSignatureAsync(folder);
            return Ok(new GeneralResponse<CloudinarySignatureResponse>
            {
                IsSuccess = true,
                Message = "Get signature successfully",
                Data = result,
                StatusCode = 200
            });
        }

        [HttpPost("upload-image")]
        public async Task<IActionResult> UploadImage(IFormFile file, [FromQuery] string folder = "images")
        {
            var result = await _mediaService.UploadImageAsync(file, folder);
            return Ok(new GeneralResponse<MediaUploadResponse>
            {
                IsSuccess = true,
                Message = "Upload image thành công",
                Data = result,
                StatusCode = 200
            });
        }

        // --- 2. UPLOAD MỘT VIDEO ---
        [HttpPost("upload-video")]
        public async Task<IActionResult> UploadVideo(IFormFile file, [FromQuery] string folder = "videos")
        {
            var result = await _mediaService.UploadVideoAsync(file, folder);
            return Ok(new GeneralResponse<MediaUploadResponse>
            {
                IsSuccess = true,
                Message = "Upload image thành công",
                Data = result,
                StatusCode = 200
            });

        }

        // --- XÓA FILE ---
        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteMedia([FromQuery] string publicId, [FromQuery] string resourceType)
        {
            var isDeleted = await _mediaService.DeleteFileAsync(publicId, resourceType);

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "File deleted successfully"
            });
        }
    }
}
