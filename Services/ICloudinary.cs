namespace Kpett.ChatApp.Services
{
    public interface ICloudinary
    {
        Task<string> UploadToCloudinaryAsync(IFormFile file, string basePath, string yearFolder, string monthFolder);
    }
}
