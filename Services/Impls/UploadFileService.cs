using CloudinaryDotNet;
using CloudinaryDotNet.Actions;


namespace Kpett.ChatApp.Services.Impls
{
    /// <summary>Service upload file lên Cloudinary.</summary>
    public class UploadFileService : Interfaces.ICloudinaryService
    {
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<UploadFileService> _logger;
   
        /// <summary>Khởi tạo service với các dependencies.</summary>
        public UploadFileService(Cloudinary cloudinary, ILogger<UploadFileService> logger)
        {
            _cloudinary = cloudinary ?? throw new ArgumentNullException(nameof(cloudinary));
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<string> UploadToCloudinaryAsync(IFormFile file, string basePath, string yearFolder, string monthFolder)
        {
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("Cloudinary upload rejected because file is empty");
                throw new ArgumentNullException(nameof(file));
            }

           // avatars/2026/01
            string remoteFolder = $"{basePath}/{yearFolder}/{monthFolder}";
            _logger.LogInformation("Uploading file {FileName} to Cloudinary folder {RemoteFolder}. Size: {FileSize}", file.FileName, remoteFolder, file.Length);

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
                _logger.LogError("Cloudinary upload failed for file {FileName}: {ErrorMessage}", file.FileName, uploadResult.Error.Message);
                throw new Exception($"Cloudinary Upload Error: {uploadResult.Error.Message}");
            }

            _logger.LogInformation("Uploaded file {FileName} to Cloudinary with public ID {PublicId}", file.FileName, uploadResult.PublicId);
            return uploadResult.SecureUrl.ToString();
        }
    }
}
