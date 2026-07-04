namespace Kpett.ChatApp.Services.Interfaces
{
    /// <summary>
    /// Service upload file lên Cloudinary.
    /// </summary>
    public interface ICloudinaryService
    {
        /// <summary>Upload file lên Cloudinary theo đường dẫn năm/tháng.</summary>
        Task<string> UploadToCloudinaryAsync(IFormFile file, string basePath, string yearFolder, string monthFolder);
    }
}
