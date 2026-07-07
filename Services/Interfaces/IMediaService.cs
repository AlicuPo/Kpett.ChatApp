using Kpett.ChatApp.DTOs.Response.Media;

namespace Kpett.ChatApp.Services.Interfaces
{
    /// <summary>
    /// Service quản lý upload/xoá media lên Cloudinary và xử lý ảnh tạm.
    /// </summary>
    public interface IMediaService
    {
        /// <summary>Tạo chữ ký upload cho Cloudinary (client-side upload).</summary>
        Task<CloudinarySignatureResponse> GenerateUploadSignatureAsync(string folder = "general");

        /// <summary>Xác nhận media đã upload lên Cloudinary thành công.</summary>
        Task ConfirmMediaOnCloudinaryAsync(List<string> publicIds);

        /// <summary>Upload ảnh lên Cloudinary.</summary>
        Task<MediaUploadResponse> UploadImageAsync(IFormFile file, string folder = "general");

        /// <summary>Upload video lên Cloudinary.</summary>
        Task<MediaUploadResponse> UploadVideoAsync(IFormFile file, string folder = "videos");

        /// <summary>Xoá file khỏi Cloudinary.</summary>
        Task<bool> DeleteFileAsync(string publicId, string resourceType);

        /// <summary>Dọn dẹp ảnh tạm không còn referenced trên Cloudinary.</summary>
        Task CleanUpOrphanedImagesAsync();
    }
}
