using Kpett.ChatApp.DTOs.Response.Media;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IMediaService
    {
        Task<CloudinarySignatureResponse> GenerateUploadSignature(string folder = "general");
        Task<MediaUploadResponse> UploadImageAsync(IFormFile file, string folder = "general");

        Task<MediaUploadResponse> UploadVideoAsync(IFormFile file, string folder = "videos");

        Task<bool> DeleteFileAsync(string publicId, string resourceType);

        /// <summary>
        /// Xử lý payload webhook từ Cloudinary
        /// </summary>
        /// <param name="rawBody">Chuỗi JSON body thô</param>
        /// <param name="signature">Chữ ký từ header</param>
        /// <param name="timestampStr">Thời gian từ header</param>
        /// <returns>True nếu xử lý thành công và hợp lệ, False nếu sai chữ ký hoặc lỗi</returns>
        Task<bool> ProcessCloudinaryWebhookAsync(string rawBody, string signature, string timestampStr);
    }
}
