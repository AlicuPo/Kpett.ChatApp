using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Response.Media;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Options;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using static Kpett.ChatApp.Contants.ErrorCodes;

namespace Kpett.ChatApp.Services.Impls
{
    public class MediaService : IMediaService
    {
        private readonly Cloudinary _cloudinary;
        private readonly CloudinaryOptions _cloudinarySettings;
        private readonly MediaOptions _mediaSettings;
        private readonly AppDbContext _context;
        private readonly ILogger<MediaService> _logger;

        public MediaService(
            IOptions<CloudinaryOptions> cloudinaryConfig,
            IOptions<MediaOptions> mediaConfig,
            AppDbContext context,
            ILogger<MediaService> logger)
        {
            var acc = new Account(
                cloudinaryConfig.Value.CloudName,
                cloudinaryConfig.Value.ApiKey,
                cloudinaryConfig.Value.ApiSecret);

            _cloudinary = new Cloudinary(acc);
            _cloudinary.Api.Secure = true;

            _cloudinarySettings = cloudinaryConfig.Value;

            _mediaSettings = mediaConfig.Value;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Tạo chữ ký cho phép Client tự upload trực tiếp lên Cloudinary
        /// </summary>
        public async Task<CloudinarySignatureResponse> GenerateUploadSignature(string folder = "general")
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string publicId = $"{Guid.NewGuid():N}";
            string cloudName = _cloudinary.Api.Account.Cloud;

            var parametersToSign = new Dictionary<string, object>
            {
                { "asset_folder", folder },
                { "public_id", publicId },
                { "tags", "status_temp" },
                { "timestamp", timestamp },
            };

            string signature = _cloudinary.Api.SignParameters(parametersToSign);

            string uploadUrl = $"https://api.cloudinary.com/v1_1/{cloudName}/auto/upload";

            return new CloudinarySignatureResponse
            {
                PublicId = publicId,
                Signature = signature,
                Timestamp = timestamp,
                Folder = folder,
                CloudName = _cloudinary.Api.Account.Cloud,
                ApiKey = _cloudinary.Api.Account.ApiKey,
                UploadUrl = uploadUrl,
                Tags = "status_temp"
            };
        }

        public async Task ConfirmMediaOnCloudinaryAsync(List<string> publicIds)
        {
            if (publicIds == null || !publicIds.Any())
            {
                return;
            }

            try
            {
                var removeTagParams = new TagParams()
                {
                    Command = TagCommand.Remove,
                    Tag = "status_temp",
                    PublicIds = publicIds
                };
                await _cloudinary.TagAsync(removeTagParams);

                var addTagParams = new TagParams()
                {
                    Command = TagCommand.Add,
                    Tag = "active",
                    PublicIds = publicIds
                };
                await _cloudinary.TagAsync(addTagParams);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi confirm media trên Cloudinary cho các ID: {Ids}", string.Join(",", publicIds));
            }
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

        public async Task<bool> DeleteFileAsync(string publicId, string resourceType)
        {
            if (string.IsNullOrWhiteSpace(publicId) || string.IsNullOrWhiteSpace(resourceType))
            {
                throw new BadRequestException(VALIDATION.REQUIRED, "Thiếu publicId hoặc resourceType");
            }

            // Phân loại linh hoạt các định dạng file sang chuẩn của Cloudinary
            ResourceType cloudinaryResourceType = resourceType.ToLower() switch
            {
                "video" => ResourceType.Video,
                "audio" => ResourceType.Video,
                "raw" => ResourceType.Raw,
                "image" => ResourceType.Image,
                "auto" => ResourceType.Auto,
                _ => ResourceType.Image
            };

            var deleteParams = new DeletionParams(publicId)
            {
                ResourceType = cloudinaryResourceType
            };

            var result = await _cloudinary.DestroyAsync(deleteParams);

            return result.Result == "ok" || result.Result == "not found";
        }

        public async Task CleanUpOrphanedImagesAsync()
        {
            try
            {
                var result = await _cloudinary.DeleteResourcesByTagAsync("status_temp");

                _logger.LogInformation("Đã xóa {Count} ảnh rác trên Cloudinary.", result.Deleted.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chạy Cronjob dọn dẹp ảnh.");
            }
        }
    }
}
