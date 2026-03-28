using Kpett.ChatApp.DTOs.Response.Media;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    public class MediaController : Controller
    {
        private readonly IMediaService _mediaService;
        public MediaController(IMediaService mediaService)
        {
            _mediaService = mediaService;
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

        // --- 4. XÓA FILE ---
        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteMedia([FromQuery] string publicId)
        {
            var isDeleted = await _mediaService.DeleteFileAsync(publicId);

            return Ok(new GeneralResponse
            {
                IsSuccess = isDeleted,
                Message = isDeleted ? "Xóa file thành công" : "Không tìm thấy file hoặc xóa thất bại",
                StatusCode = isDeleted ? 200 : 500
            });
        }
    }
}
