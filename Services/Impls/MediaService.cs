using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Response.Media;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Options;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using static Kpett.ChatApp.Contants.ErrorCodes;

namespace Kpett.ChatApp.Services.Impls
{
    public class MediaService : IMediaService
    {
        private readonly Cloudinary _cloudinary;
        private readonly CloudinaryOptions _cloudinarySettings;
        private readonly MediaOptions _mediaSettings;
        private readonly AppDbContext _context;

        public MediaService(
            IOptions<CloudinaryOptions> cloudinaryConfig,
            IOptions<MediaOptions> mediaConfig,
            AppDbContext context)
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
        }

        /// <summary>
        /// Tạo chữ ký cho phép Client tự upload trực tiếp lên Cloudinary
        /// </summary>
        public CloudinarySignatureResponse GenerateUploadSignature(string folder = "general")
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var parametersToSign = new SortedDictionary<string, object>
            {
                { "timestamp", timestamp },
                { "asset_folder", folder },
                { "notification_url", _cloudinarySettings.NotificationUrl }
            };

            string signature = _cloudinary.Api.SignParameters(parametersToSign);

            return new CloudinarySignatureResponse
            {
                Signature = signature,
                Timestamp = timestamp,
                Folder = folder,
                CloudName = _cloudinary.Api.Account.Cloud,
                ApiKey = _cloudinary.Api.Account.ApiKey,
                NotificationUrl = _cloudinarySettings.NotificationUrl
            };
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
            if (string.IsNullOrEmpty(publicId) || string.IsNullOrEmpty(resourceType))
            {
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Thiếu publicId hoặc resourceType");
            }

            var deleteParams = new DeletionParams(publicId)
            {
                ResourceType = resourceType.ToLower() == "video"
                    ? ResourceType.Video
                    : ResourceType.Image
            };

            var result = await _cloudinary.DestroyAsync(deleteParams);

            return result.Result == "ok";
        }

        public async Task<bool> ProcessCloudinaryWebhookAsync(string rawBody, string signature, string timestampStr)
        {
            // Ép kiểu timestamp an toàn
            if (!long.TryParse(timestampStr, out long timestamp))
            {
                return false;
            }

            // Xác minh chữ ký với thời hạn 7200 giây
            bool isValid = _cloudinary.Api.VerifyNotificationSignature(rawBody, timestamp, signature, 7200);
            if (!isValid)
            {
                return false;
            }

            // Parse JSON thành Object
            var payload = JsonSerializer.Deserialize<CloudinaryNotificationPayload>(rawBody);
            if (payload == null) return false;

            // Xử lý logic Database dựa trên sự kiện
            switch (payload.notification_type)
            {
                case "upload":
                    await HandleUploadEventAsync(payload);
                    break;

                case "delete":
                    await HandleDeleteEventAsync(payload);
                    break;

                default:
                    break;
            }

            return true;
        }

        // --- CÁC HÀM TRỢ GIÚP (PRIVATE) ---

        private async Task HandleUploadEventAsync(CloudinaryNotificationPayload payload)
        {
            MediaType mediaType = payload.resource_type.ToLower() switch
            {
                "image" => MediaType.Image,
                "video" => MediaType.Video,
                "raw" => MediaType.Document,
                _ => MediaType.Unknown
            };

            var existingMedia = await _context.PostMedia.AnyAsync(m => m.Id == payload.public_id);

            if(!existingMedia)
            {
                var entity = new PostMedia
                {
                    Id = payload.public_id,
                    MediaUrl = payload.secure_url,
                    MediaType = mediaType.GetDescription(),
                    IsTemporary = true,
                    CreatedAt = DateTime.Now,
                };
                _context.PostMedia.Add(entity);
            }

            await _context.SaveChangesAsync();
        }

        private async Task HandleDeleteEventAsync(CloudinaryNotificationPayload payload)
        {
            var entity = await _context.PostMedia.FirstOrDefaultAsync(m => m.Id == payload.public_id);

            if (entity != null)
            {
                _context.PostMedia.Remove(entity);
                await _context.SaveChangesAsync();
            }

        }
    }
}
