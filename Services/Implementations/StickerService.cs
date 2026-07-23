using Kpett.ChatApp.Data;
using Kpett.ChatApp.DTOs.Request.Sticker;
using Kpett.ChatApp.DTOs.Response.Sticker;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Services.Implementations
{
    public class StickerService : IStickerService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<StickerService> _logger;

        public StickerService(AppDbContext context, ILogger<StickerService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<StickerPackResponse> CreateStickerPackAsync(string currentUserId, CreateStickerPackRequest request, CancellationToken cancel)
        {
            var pack = new StickerPack
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Description = request.Description,
                OwnerId = currentUserId,
                IsPublic = request.IsPublic,
                CreatedAt = DateTime.UtcNow
            };

            _context.StickerPacks.Add(pack);
            await _context.SaveChangesAsync(cancel);

            return MapToPackResponse(pack, 0);
        }

        public async Task<StickerResponse> AddStickerToPackAsync(string currentUserId, string packId, string mediaUrl, string? publicId, string? emoji, CancellationToken cancel)
        {
            var pack = await _context.StickerPacks.FirstOrDefaultAsync(p => p.Id == packId, cancel);
            if (pack == null)
                throw new NotFoundException("STICKER.PACK_NOT_FOUND", "Sticker pack not found.");

            if (pack.OwnerId != currentUserId)
                throw new ForbiddenException("STICKER.FORBIDDEN", "You can only add stickers to your own packs.");

            var sticker = new Sticker
            {
                Id = Guid.NewGuid().ToString(),
                StickerPackId = packId,
                MediaUrl = mediaUrl,
                PublicId = publicId,
                Emoji = emoji,
                CreatedAt = DateTime.UtcNow
            };

            _context.Stickers.Add(sticker);

            if (pack.ThumbnailUrl == null)
                pack.ThumbnailUrl = mediaUrl;

            await _context.SaveChangesAsync(cancel);

            return MapToStickerResponse(sticker);
        }

        public async Task<List<StickerPackResponse>> GetMyStickerPacksAsync(string currentUserId, CancellationToken cancel)
        {
            var packs = await _context.StickerPacks
                .AsNoTracking()
                .Where(p => p.OwnerId == currentUserId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(cancel);

            var packIds = packs.Select(p => p.Id).ToList();
            var stickerCounts = await _context.Stickers
                .AsNoTracking()
                .Where(s => packIds.Contains(s.StickerPackId))
                .GroupBy(s => s.StickerPackId)
                .Select(g => new { PackId = g.Key, Count = g.Count() })
                .ToListAsync(cancel);

            var countMap = stickerCounts.ToDictionary(g => g.PackId, g => g.Count);

            return packs.Select(p => MapToPackResponse(p, countMap.GetValueOrDefault(p.Id, 0))).ToList();
        }

        public async Task<List<StickerPackResponse>> GetPublicStickerPacksAsync(CancellationToken cancel)
        {
            var packs = await _context.StickerPacks
                .AsNoTracking()
                .Where(p => p.IsPublic)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(cancel);

            var packIds = packs.Select(p => p.Id).ToList();
            var stickers = await _context.Stickers
                .AsNoTracking()
                .Where(s => packIds.Contains(s.StickerPackId))
                .ToListAsync(cancel);

            var stickerGroup = stickers.GroupBy(s => s.StickerPackId).ToDictionary(g => g.Key, g => g.ToList());

            return packs.Select(p =>
            {
                var response = MapToPackResponse(p, stickerGroup.GetValueOrDefault(p.Id, new List<Sticker>())?.Count ?? 0);
                if (stickerGroup.TryGetValue(p.Id, out var packStickers))
                {
                    response.Stickers = packStickers.Select(MapToStickerResponse).ToList();
                }
                return response;
            }).ToList();
        }

        public async Task<bool> DeleteStickerPackAsync(string currentUserId, string packId, CancellationToken cancel)
        {
            var pack = await _context.StickerPacks.FirstOrDefaultAsync(p => p.Id == packId, cancel);
            if (pack == null) return false;

            if (pack.OwnerId != currentUserId)
                throw new ForbiddenException("STICKER.FORBIDDEN", "You can only delete your own packs.");

            var stickers = await _context.Stickers.Where(s => s.StickerPackId == packId).ToListAsync(cancel);
            _context.Stickers.RemoveRange(stickers);
            _context.StickerPacks.Remove(pack);
            await _context.SaveChangesAsync(cancel);

            return true;
        }

        public async Task<bool> DeleteStickerAsync(string currentUserId, string stickerId, CancellationToken cancel)
        {
            var sticker = await _context.Stickers.FirstOrDefaultAsync(s => s.Id == stickerId, cancel);
            if (sticker == null) return false;

            var pack = await _context.StickerPacks.FirstOrDefaultAsync(p => p.Id == sticker.StickerPackId, cancel);
            if (pack == null || pack.OwnerId != currentUserId)
                throw new ForbiddenException("STICKER.FORBIDDEN", "You can only delete stickers from your own packs.");

            _context.Stickers.Remove(sticker);
            await _context.SaveChangesAsync(cancel);

            return true;
        }

        private static StickerPackResponse MapToPackResponse(StickerPack pack, int stickerCount)
        {
            return new StickerPackResponse
            {
                Id = pack.Id,
                Name = pack.Name,
                Description = pack.Description,
                ThumbnailUrl = pack.ThumbnailUrl,
                OwnerId = pack.OwnerId,
                IsPublic = pack.IsPublic,
                StickerCount = stickerCount,
                CreatedAt = pack.CreatedAt
            };
        }

        private static StickerResponse MapToStickerResponse(Sticker sticker)
        {
            return new StickerResponse
            {
                Id = sticker.Id,
                StickerPackId = sticker.StickerPackId,
                MediaUrl = sticker.MediaUrl,
                PublicId = sticker.PublicId,
                Emoji = sticker.Emoji,
                Width = sticker.Width,
                Height = sticker.Height,
                FileSize = sticker.FileSize,
                CreatedAt = sticker.CreatedAt
            };
        }
    }
}
