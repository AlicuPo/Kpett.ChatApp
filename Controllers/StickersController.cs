using Kpett.ChatApp.DTOs.Request.Sticker;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.DTOs.Response.Sticker;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StickersController : ControllerBase
    {
        private readonly IStickerService _stickerService;

        public StickersController(IStickerService stickerService)
        {
            _stickerService = stickerService;
        }

        [HttpPost("packs")]
        public async Task<IActionResult> CreatePack([FromBody] CreateStickerPackRequest request, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            var result = await _stickerService.CreateStickerPackAsync(currentUserId, request, cancel);

            return Created($"/api/stickers/packs/{result.Id}", new GeneralResponse<StickerPackResponse>
            {
                IsSuccess = true,
                Message = "Sticker pack created successfully",
                Data = result,
                StatusCode = StatusCodes.Status201Created
            });
        }

        [HttpPost("packs/{packId}/stickers")]
        public async Task<IActionResult> AddSticker(
            [FromRoute] string packId,
            [FromBody] AddStickerRequest request,
            CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            var result = await _stickerService.AddStickerToPackAsync(currentUserId, packId, request.MediaUrl, request.PublicId, request.Emoji, cancel);

            return Ok(new GeneralResponse<StickerResponse>
            {
                IsSuccess = true,
                Message = "Sticker added successfully",
                Data = result,
                StatusCode = StatusCodes.Status200OK
            });
        }

        [HttpGet("packs/my")]
        public async Task<IActionResult> GetMyPacks(CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            var result = await _stickerService.GetMyStickerPacksAsync(currentUserId, cancel);

            return Ok(new GeneralResponse<List<StickerPackResponse>>
            {
                IsSuccess = true,
                Message = "Get my sticker packs successfully",
                Data = result,
                StatusCode = StatusCodes.Status200OK
            });
        }

        [HttpGet("packs/public")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicPacks(CancellationToken cancel)
        {
            var result = await _stickerService.GetPublicStickerPacksAsync(cancel);

            return Ok(new GeneralResponse<List<StickerPackResponse>>
            {
                IsSuccess = true,
                Message = "Get public sticker packs successfully",
                Data = result,
                StatusCode = StatusCodes.Status200OK
            });
        }

        [HttpDelete("packs/{packId}")]
        public async Task<IActionResult> DeletePack([FromRoute] string packId, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            var deleted = await _stickerService.DeleteStickerPackAsync(currentUserId, packId, cancel);

            return Ok(new GeneralResponse
            {
                IsSuccess = deleted,
                Message = deleted ? "Sticker pack deleted" : "Sticker pack not found",
                StatusCode = StatusCodes.Status200OK
            });
        }

        [HttpDelete("stickers/{stickerId}")]
        public async Task<IActionResult> DeleteSticker([FromRoute] string stickerId, CancellationToken cancel)
        {
            var currentUserId = User.GetRequiredUserId();
            var deleted = await _stickerService.DeleteStickerAsync(currentUserId, stickerId, cancel);

            return Ok(new GeneralResponse
            {
                IsSuccess = deleted,
                Message = deleted ? "Sticker deleted" : "Sticker not found",
                StatusCode = StatusCodes.Status200OK
            });
        }
    }
}
