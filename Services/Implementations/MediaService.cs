using Kpett.ChatApp.Constants;
using Kpett.ChatApp.DTOs.Response.Media;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Options;
using Kpett.ChatApp.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Kpett.ChatApp.Services.Implementations
{
    public class MediaService : IMediaService
    {
        private readonly MediaOptions _mediaSettings;
        private readonly ILogger<MediaService> _logger;
        private readonly IWebHostEnvironment _env;

        public MediaService(
            IOptions<MediaOptions> mediaConfig,
            ILogger<MediaService> logger,
            IWebHostEnvironment env)
        {
            _mediaSettings = mediaConfig.Value;
            _logger = logger;
            _env = env;
        }

        public async Task<MediaUploadResponse> UploadAsync(IFormFile file, string folder)
        {
            var isVideo = file.ContentType.StartsWith("video/");
            var maxSize = isVideo ? _mediaSettings.MaxVideoSizeBytes : _mediaSettings.MaxImageSizeBytes;
            var allowedExtensions = isVideo ? _mediaSettings.AllowedVideoExtensions : _mediaSettings.AllowedImageExtensions;

            ValidateFile(file, maxSize, allowedExtensions);

            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", folder);
            Directory.CreateDirectory(uploadsDir);

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"/uploads/{folder}/{fileName}";

            _logger.LogInformation("Saved file {FileName} -> {FilePath} ({Size} bytes)", file.FileName, filePath, file.Length);

            return new MediaUploadResponse
            {
                PublicId = fileName,
                SecureUrl = url,
                Format = ext.TrimStart('.'),
                Bytes = file.Length
            };
        }

        public Task<bool> DeleteAsync(string fileUrl)
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "File URL is required");

            var uri = new Uri(fileUrl, UriKind.RelativeOrAbsolute);
            var relativePath = uri.IsAbsoluteUri ? uri.AbsolutePath.TrimStart('/') : fileUrl.TrimStart('/');
            var fullPath = Path.Combine(_env.WebRootPath, relativePath);

            if (!fullPath.StartsWith(_env.WebRootPath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Path traversal attempt blocked: {FileUrl}", fileUrl);
                throw new BadRequestException(ErrorCodes.VALIDATION.INVALID, "Invalid file path");
            }

            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("File not found for deletion: {FilePath}", fullPath);
                return Task.FromResult(false);
            }

            File.Delete(fullPath);
            _logger.LogInformation("Deleted file: {FilePath}", fullPath);
            return Task.FromResult(true);
        }

        private static void ValidateFile(IFormFile file, long maxSize, string[] allowedExtensions)
        {
            if (file == null || file.Length == 0)
                throw new BadRequestException(ErrorCodes.MEDIA.FILE_EMPTY, "File is empty");

            if (file.Length > maxSize)
                throw new BadRequestException(ErrorCodes.MEDIA.FILE_SIZE_EXCEEDS_LIMIT, "File size exceeds limit");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
                throw new BadRequestException(ErrorCodes.MEDIA.INVALID_FILE_EXTENSION, "Invalid file format");
        }
    }
}
