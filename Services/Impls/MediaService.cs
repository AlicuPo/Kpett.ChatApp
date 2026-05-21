using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Kpett.ChatApp.Constants;
using Kpett.ChatApp.DTOs.Response.Media;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Options;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using static Kpett.ChatApp.Constants.ErrorCodes;

namespace Kpett.ChatApp.Services.Impls
{
    public class MediaService : IMediaService
    {
        private readonly Cloudinary _cloudinary;
        private readonly MediaOptions _mediaSettings;
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

            _mediaSettings = mediaConfig.Value;
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
            _logger.LogInformation("Generating Cloudinary upload signature for folder {Folder} with public ID {PublicId}", folder, publicId);

            var parametersToSign = new Dictionary<string, object>
            {
                { "asset_folder", folder },
                { "public_id", publicId },
                { "tags", "status_temp" },
                { "timestamp", timestamp },
            };

            string signature = _cloudinary.Api.SignParameters(parametersToSign);

            string uploadUrl = $"https://api.cloudinary.com/v1_1/{cloudName}/auto/upload";

            _logger.LogInformation("Generated Cloudinary upload signature for public ID {PublicId}", publicId);
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
                _logger.LogDebug("Skipping Cloudinary media confirmation because public ID list is empty");
                return;
            }

            try
            {
                _logger.LogInformation("Confirming {MediaCount} Cloudinary media resources", publicIds.Count);

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
                _logger.LogInformation("Confirmed {MediaCount} Cloudinary media resources", publicIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi confirm media trên Cloudinary cho các ID: {Ids}", string.Join(",", publicIds));
            }
        }

        public async Task<MediaUploadResponse> UploadImageAsync(IFormFile file, string folder = "general")
        {
            ValidateFile(file, _mediaSettings.MaxImageSizeBytes, _mediaSettings.AllowedImageExtensions);
            _logger.LogInformation("Uploading image {FileName} to Cloudinary folder {Folder}. Size: {FileSize}", file.FileName, folder, file.Length);

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

            if (uploadResult.Error != null)
            {
                _logger.LogError("Cloudinary image upload failed for file {FileName}: {ErrorMessage}", file.FileName, uploadResult.Error.Message);
                throw new Exception(MEDIA.UPLOAD_FAILED);
            }

            _logger.LogInformation("Uploaded image {FileName} to Cloudinary with public ID {PublicId}", file.FileName, uploadResult.PublicId);
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
            _logger.LogInformation("Uploading video {FileName} to Cloudinary folder {Folder}. Size: {FileSize}", file.FileName, folder, file.Length);

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
            {
                _logger.LogError("Cloudinary video upload failed for file {FileName}: {ErrorMessage}", file.FileName, uploadResult.Error.Message);
                throw new Exception($"Lỗi upload video từ Cloudinary: {uploadResult.Error.Message}");
            }

            _logger.LogInformation("Uploaded video {FileName} to Cloudinary with public ID {PublicId}", file.FileName, uploadResult.PublicId);
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
            {
                _logger.LogWarning("Media validation failed because file is empty");
                throw new BadRequestException(ErrorCodes.MEDIA.FILE_EMPTY, "File is empty");
            }

            if (file.Length > maxSize)
            {
                _logger.LogWarning("Media validation failed for file {FileName}. Size {FileSize} exceeds limit {MaxSize}", file.FileName, file.Length, maxSize);
                throw new BadRequestException(ErrorCodes.MEDIA.FILE_SIZE_EXCEEDS_LIMIT, "File size exceeds limit");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
            {
                _logger.LogWarning("Media validation failed for file {FileName}. Extension {Extension} is not allowed", file.FileName, extension);
                throw new BadRequestException(ErrorCodes.MEDIA.INVALID_FILE_EXTENSION, "Invalid file format");
            }
        }

        public async Task<bool> DeleteFileAsync(string publicId, string resourceType)
        {
            if (string.IsNullOrWhiteSpace(publicId) || string.IsNullOrWhiteSpace(resourceType))
            {
                _logger.LogWarning("Cloudinary delete rejected because public ID or resource type is empty");
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

            var deleted = result.Result == "ok" || result.Result == "not found";
            _logger.LogInformation("Cloudinary delete completed for public ID {PublicId}. ResourceType: {ResourceType}. Result: {Result}", publicId, resourceType, result.Result);
            return deleted;
        }

        public async Task CleanUpOrphanedImagesAsync()
        {
            try
            {
                _logger.LogInformation("Starting Cloudinary orphaned image cleanup");
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

