using Kpett.ChatApp.DTOs.Payload.Cursor;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Notidication;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Extensions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Kpett.ChatApp.Be.Services.Impls
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(AppDbContext context, ILogger<NotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<PaginatedData<NotificationResponse>> GetUserNotificationsAsync(string currentUserId, CursorPaginationRequest request, CancellationToken cancel)
        {
            var limit = request.Limit <= 0 ? 20 : Math.Min(request.Limit, 50);

            DateTime? cursorDate = null;
            string? cursorId = null;

            if (!string.IsNullOrWhiteSpace(request.Cursor))
            {
                var decoded = CursorHelper.Decode<NotificationCursorPayload>(request.Cursor);
                if (decoded != null)
                {
                    cursorDate = decoded.CreatedAt;
                    cursorId = decoded.NotificationId;
                }
            }

            var query = _context.Notifications.AsNoTracking()
                .Where(n => n.RecipientId == currentUserId);

            // Phân trang bằng Cursor (Cũ hơn mốc Cursor)
            if (cursorDate.HasValue && !string.IsNullOrWhiteSpace(cursorId))
            {
                query = query.Where(n => n.CreatedAt < cursorDate.Value ||
                                        (n.CreatedAt == cursorDate.Value && string.Compare(n.Id, cursorId) < 0));
            }

            var rawData = await query
                .OrderByDescending(n => n.CreatedAt)
                .ThenByDescending(n => n.Id)
                .Take(limit + 1)
                .Select(n => new
                {
                    n.Id,
                    n.Type,
                    n.ReferenceId,
                    n.Metadata,
                    n.IsRead,
                    n.CreatedAt,
                    ActorInfo = _context.Users
                        .Where(u => u.Id == n.ActorId)
                        .Select(u => new
                        {
                            u.Id,
                            DisplayName = u.DisplayName ?? u.Username,
                            u.Username,
                            AvatarUrl = _context.UserMedias
                                .Where(um => um.UserId == u.Id && um.IsPrimary && um.MediaType == "Avatar")
                                .Select(um => um.MediaUrl)
                                .FirstOrDefault()
                        })
                        .FirstOrDefault()
                })
                .ToListAsync(cancel);

            string? nextCursor = null;
            if (rawData.Count > limit)
            {
                var lastItem = rawData[limit - 1];
                nextCursor = CursorHelper.Encode(new NotificationCursorPayload
                {
                    NotificationId = lastItem.Id,
                    CreatedAt = lastItem.CreatedAt
                });
                rawData.RemoveAt(limit);
            }

            var items = rawData.Select(n => new NotificationResponse
            {
                Id = n.Id,
                Type = n.Type,
                ReferenceId = n.ReferenceId,
                Metadata = string.IsNullOrWhiteSpace(n.Metadata) ? null : JsonSerializer.Deserialize<object>(n.Metadata),
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt.ToUtc(),
                Actor = n.ActorInfo == null ? null : new ActorSnippetResponse
                {
                    Id = n.ActorInfo.Id,
                    DisplayName = n.ActorInfo.DisplayName,
                    Username = n.ActorInfo.Username,
                    AvatarUrl = n.ActorInfo.AvatarUrl
                }
            }).ToList();

            _logger.LogInformation("User {UserId} retrieved {Count} notifications", currentUserId, items.Count);
            return new PaginatedData<NotificationResponse>
            {
                Items = items,
                Pagination = new CursorPaginationMeta { NextCursor = nextCursor, Limit = limit }
            };
        }

        public async Task<int> GetUnreadCountAsync(string currentUserId, CancellationToken cancel)
        {
            var count = await _context.Notifications
                .CountAsync(n => n.RecipientId == currentUserId && !n.IsRead, cancel);
            _logger.LogInformation("User {UserId} has {UnreadCount} unread notifications", currentUserId, count);
            return count;
        }

        public async Task MarkAsReadAsync(string currentUserId, string notificationId, CancellationToken cancel)
        {
            // Tối ưu hóa: Dùng ExecuteUpdateAsync ghi trực tiếp xuống SQL Server không cần Load Entity
            var affectedRows = await _context.Notifications
                .Where(n => n.Id == notificationId && n.RecipientId == currentUserId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), cancel);
            _logger.LogInformation("User {UserId} marked notification {NotificationId} as read. Affected rows: {AffectedRows}", currentUserId, notificationId, affectedRows);
        }

        public async Task MarkAllAsReadAsync(string currentUserId, CancellationToken cancel)
        {
            // Tối ưu hóa: Đánh dấu tất cả đã đọc chỉ với 1 câu lệnh SQL
            var affectedRows = await _context.Notifications
                .Where(n => n.RecipientId == currentUserId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), cancel);
            _logger.LogInformation("User {UserId} marked all notifications as read. Affected rows: {AffectedRows}", currentUserId, affectedRows);
        }
    }
}
