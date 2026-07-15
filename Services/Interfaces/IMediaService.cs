using Kpett.ChatApp.DTOs.Response.Media;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IMediaService
    {
        Task<MediaUploadResponse> UploadAsync(IFormFile file, string folder);
        Task<bool> DeleteAsync(string fileUrl);
    }
}
