using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Response.Media;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Options;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.Extensions.Options;
using static Kpett.ChatApp.Contants.ErrorCodes;

namespace Kpett.ChatApp.Services.Impls
{
    public class MediaService : IMediaService
    {
        private readonly Cloudinary _cloudinary;
        private readonly MediaOptions _mediaSettings;

        public MediaService(
            IOptions<CloudinaryOptions> cloudinaryConfig,
            IOptions<MediaOptions> mediaConfig)
        {
            var acc = new Account(
                cloudinaryConfig.Value.CloudName,
                cloudinaryConfig.Value.ApiKey,
                cloudinaryConfig.Value.ApiSecret);

            _cloudinary = new Cloudinary(acc);
            _cloudinary.Api.Secure = true;

            _mediaSettings = mediaConfig.Value;
        }

        public async Task<MediaUploadResponse> UploadImageAsync(IFormFile file, string folder = "general")
        {
            ValidateFile(file, _mediaSettings.MaxImageSizeBytes, _mediaSettings.AllowedImageExtensions);

            var uploadResult = new ImageUploadResult();
            using (var stream = file.OpenReadStream())
            {
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = folder,
                    Transformation = new Transformation().Quality("auto").FetchFormat("auto")
                };
                uploadResult = await _cloudinary.UploadAsync(uploadParams);
            }

            if (uploadResult.Error != null) throw new Exception(MEDIA.UPLOAD_FAILED);

            return new MediaUploadResponse
            {
                PublicId = uploadResult.PublicId,
                SecureUrl = uploadResult.SecureUrl.ToString(),
                Format = uploadResult.Format,
                Bytes = uploadResult.Bytes
            };
        }

        public async Task<MediaUploadResponse> UploadVideoAsync(IFormFile file, string folder = "videos")
        {
            ValidateFile(file, _mediaSettings.MaxVideoSizeBytes, _mediaSettings.AllowedVideoExtensions);

            var uploadResult = new VideoUploadResult();
            using (var stream = file.OpenReadStream())
            {
                var uploadParams = new VideoUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = folder
                };
                uploadResult = await _cloudinary.UploadAsync(uploadParams);
            }

            if (uploadResult.Error != null)
                throw new Exception($"Lỗi upload video từ Cloudinary: {uploadResult.Error.Message}");

            return new MediaUploadResponse
            {
                PublicId = uploadResult.PublicId,
                SecureUrl = uploadResult.SecureUrl.ToString(),
                Format = uploadResult.Format,
                Bytes = uploadResult.Bytes
            };
        }

        private void ValidateFile(IFormFile file, long maxSize, string[] allowedExtensions)
        {
            if (file == null || file.Length == 0)
                throw new BadRequestException(ErrorCodes.MEDIA.FILE_EMPTY, "File is empty");

            if (file.Length > maxSize)
                throw new BadRequestException(ErrorCodes.MEDIA.FILE_SIZE_EXCEEDS_LIMIT, "File size exceeds limit");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
                throw new BadRequestException(ErrorCodes.MEDIA.INVALID_FILE_EXTENSION, "Invalid file format");
        }

        public async Task<bool> DeleteFileAsync(string publicId)
        {
            var deleteParams = new DeletionParams(publicId);

            // Mặc định DeletionParams dùng cho image, nếu là video cần set resource type
            // deleteParams.ResourceType = ResourceType.Video; 

            var result = await _cloudinary.DestroyAsync(deleteParams);

            return result.Result == "ok";
        }
    }
}
