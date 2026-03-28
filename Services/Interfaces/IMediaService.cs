using Kpett.ChatApp.DTOs.Response.Media;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IMediaService
    {
        Task<MediaUploadResponse> UploadImageAsync(IFormFile file, string folder = "general");

        Task<MediaUploadResponse> UploadVideoAsync(IFormFile file, string folder = "videos");

        Task<bool> DeleteFileAsync(string publicId);
    }
}
