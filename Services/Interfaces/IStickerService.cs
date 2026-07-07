using Kpett.ChatApp.DTOs.Request.Sticker;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.DTOs.Response.Sticker;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IStickerService
    {
        Task<StickerPackResponse> CreateStickerPackAsync(string currentUserId, CreateStickerPackRequest request, CancellationToken cancel);

        Task<StickerResponse> AddStickerToPackAsync(string currentUserId, string packId, string mediaUrl, string? publicId, string? emoji, CancellationToken cancel);

        Task<List<StickerPackResponse>> GetMyStickerPacksAsync(string currentUserId, CancellationToken cancel);

        Task<List<StickerPackResponse>> GetPublicStickerPacksAsync(CancellationToken cancel);

        Task<bool> DeleteStickerPackAsync(string currentUserId, string packId, CancellationToken cancel);

        Task<bool> DeleteStickerAsync(string currentUserId, string stickerId, CancellationToken cancel);
    }
}
