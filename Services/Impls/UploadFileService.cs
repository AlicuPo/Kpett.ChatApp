using CloudinaryDotNet;
using CloudinaryDotNet.Actions;


namespace Kpett.ChatApp.Services.Impls
{
    public class UploadFileService : Interfaces.ICloudinaryService
    {
        private readonly Cloudinary _cloudinary;
   
        public UploadFileService(Cloudinary cloudinary)
        {
            _cloudinary = cloudinary ?? throw new ArgumentNullException(nameof(cloudinary));
        }

        public async Task<string> UploadToCloudinaryAsync(IFormFile file, string basePath, string yearFolder, string monthFolder)
        {
            if (file == null || file.Length == 0) throw new ArgumentNullException(nameof(file));

           // avatars/2026/01
            string remoteFolder = $"{basePath}/{yearFolder}/{monthFolder}";

            using var stream = file.OpenReadStream();

            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(file.FileName, stream),
                Folder = remoteFolder,
             
                Transformation = new Transformation()
                    .Width(500).Height(500).Crop("thumb").Gravity("face").Quality("auto").FetchFormat("auto")
            };
            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                throw new Exception($"Cloudinary Upload Error: {uploadResult.Error.Message}");
            }
            return uploadResult.SecureUrl.ToString();
        }
    }
}
