namespace Kpett.ChatApp.Services.Interfaces
{
    public interface ICloudinaryService
    {
        Task<string> UploadToCloudinaryAsync(IFormFile file, string basePath, string yearFolder, string monthFolder);
    }
}
