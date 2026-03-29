using Microsoft.AspNetCore.Mvc;
using Kpett.ChatApp.Services.Interfaces;
using Kpett.ChatApp.Contants;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.DTOs.Response.Shared;

namespace Kpett.ChatApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebhookController : ControllerBase
    {
        private readonly IMediaService _mediaService;

        public WebhookController(IMediaService mediaService)
        {
            _mediaService = mediaService;
        }

        [HttpPost("cloudinary")]
        public async Task<IActionResult> CloudinaryWebhook()
        {
            if (!Request.Headers.TryGetValue("X-Cld-Signature", out var signatureValues) ||
                    !Request.Headers.TryGetValue("X-Cld-Timestamp", out var timestampValues))
            {
                throw new BadRequestException(ErrorCodes.CLOUDINARY.MISSING_HEADER, "Missing Cloudinary Webhook Headers");
            }

            // Đọc thô Body của request
            using var reader = new StreamReader(Request.Body);
            var rawBody = await reader.ReadToEndAsync();

            // Đẩy sang Service xử lý
            var success = await _mediaService.ProcessCloudinaryWebhookAsync(
                rawBody,
                signatureValues.ToString(),
                timestampValues.ToString()
            );

            if (!success)
            {
                throw new UnauthorizedException(ErrorCodes.CLOUDINARY.INVALID_SIGNATURE, "Invalid webhook signature or payload!");
            }

            return Ok(new GeneralResponse
            {
                IsSuccess = true,
                Message = "Webhook processed successfully",
                StatusCode = 200
            });
        }
    }
}