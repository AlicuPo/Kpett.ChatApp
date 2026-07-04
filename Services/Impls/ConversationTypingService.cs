using Kpett.ChatApp.DTOs.Response.Conversation;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Extensions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Hubs;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace Kpett.ChatApp.Services.Impls
{
    /// <summary>Service quản lý trạng thái typing trong hội thoại (dùng Redis + SignalR).</summary>
    public class ConversationTypingService : IConversationTypingService
    {
        private readonly IRedisService _redis;
        private readonly IHubContext<AppHub> _hubContext;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<ConversationTypingService> _logger;

        // Thời gian tự động dừng typing nếu không nhận được event mới
        private static readonly TimeSpan TypingTtl = TimeSpan.FromSeconds(5);

        // Key: "conversationId|userId|connectionId" → CTS để có thể cancel auto-expire cũ
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _expireTimers
            = new(StringComparer.Ordinal);

        /// <summary>Khởi tạo service với các dependencies.</summary>
        public ConversationTypingService(
            IRedisService redis,
            IHubContext<AppHub> hubContext,
            AppDbContext dbContext,
            ILogger<ConversationTypingService> logger)
        {
            _redis = redis;
            _hubContext = hubContext;
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<bool> StartTypingAsync(string conversationId, string userId, string connectionId, CancellationToken ct = default)
        {
            // Kiểm tra xem user đã có tab nào đang typing chưa (để quyết định có broadcast start không)
            var typingUsers = await _redis.GetTypingUsersInConversationAsync(conversationId);
            var userAlreadyTypingFromOtherTab = typingUsers.Any(t =>
                t.UserId == userId && t.ConnectionId != connectionId);
            var thisConnectionAlreadyTyping = typingUsers.Any(t =>
                t.UserId == userId && t.ConnectionId == connectionId);

            // SET/REFRESH Redis SortedSet entry với TTL mới
            await _redis.SetUserTypingAsync(conversationId, userId, connectionId, TypingTtl);

            // Hủy timer cũ (nếu có) và tạo timer mới
            RefreshExpireTimer(conversationId, userId, connectionId);

            // Broadcast start chỉ khi: connection này chưa typing VÀ không có tab nào khác đang typing
            // (tránh broadcast trùng khi user mở nhiều tab)
            var shouldBroadcastStart = !thisConnectionAlreadyTyping && !userAlreadyTypingFromOtherTab;
            _logger.LogDebug(
                "Start typing for user {UserId}, connection {ConnectionId}, conversation {ConversationId}. ShouldBroadcast: {ShouldBroadcast}",
                userId,
                connectionId,
                conversationId,
                shouldBroadcastStart);
            return shouldBroadcastStart;
        }

        /// <inheritdoc />
        public async Task<bool> StopTypingAsync(string conversationId, string userId, string connectionId, CancellationToken ct = default)
        {
            // Hủy auto-expire timer nếu đang chạy
            CancelExpireTimer(conversationId, userId, connectionId);

            await _redis.RemoveUserTypingAsync(conversationId, userId, connectionId);

            // Chỉ broadcast stop nếu không còn tab nào khác của user này đang typing
            var hasOtherConnections = await _redis.HasOtherTypingConnectionsAsync(conversationId, userId, connectionId);
            var shouldBroadcastStop = !hasOtherConnections;
            _logger.LogDebug(
                "Stop typing for user {UserId}, connection {ConnectionId}, conversation {ConversationId}. ShouldBroadcast: {ShouldBroadcast}",
                userId,
                connectionId,
                conversationId,
                shouldBroadcastStop);
            return shouldBroadcastStop;
        }

        /// <inheritdoc />
        public Task<List<(string UserId, string ConnectionId)>> GetTypingUsersAsync(string conversationId)
            => _redis.GetTypingUsersInConversationAsync(conversationId);

        /// <inheritdoc />
        public async Task<List<(string ConversationId, string UserId)>> CleanupConnectionTypingAsync(string connectionId)
        {
            // Hủy tất cả timer liên quan đến connectionId này
            var keysToCancel = _expireTimers.Keys
                .Where(k => k.EndsWith($"|{connectionId}", StringComparison.Ordinal))
                .ToList();

            foreach (var key in keysToCancel)
            {
                if (_expireTimers.TryRemove(key, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                }
            }

            var removedEntries = await _redis.RemoveAllTypingForConnectionAsync(connectionId);
            _logger.LogDebug("Cleaned up {TypingEntryCount} typing entries for connection {ConnectionId}", removedEntries.Count, connectionId);
            return removedEntries;
        }

        // Private helpers

        /// <summary>
        /// Tạo (hoặc reset) một delayed task sẽ broadcast isTyping=false sau TypingTtl.
        /// </summary>
        private void RefreshExpireTimer(string conversationId, string userId, string connectionId)
        {
            var key = TimerKey(conversationId, userId, connectionId);

            // Cancel và replace CTS cũ (nếu có)
            var newCts = new CancellationTokenSource();
            if (_expireTimers.TryRemove(key, out var oldCts))
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }
            _expireTimers[key] = newCts;

            // Fire-and-forget: chạy auto-expire sau TTL
            _ = RunAutoExpireAsync(conversationId, userId, connectionId, newCts.Token);
            _logger.LogDebug("Refreshed typing auto-expire timer for user {UserId}, connection {ConnectionId}, conversation {ConversationId}", userId, connectionId, conversationId);
        }

        private void CancelExpireTimer(string conversationId, string userId, string connectionId)
        {
            var key = TimerKey(conversationId, userId, connectionId);
            if (_expireTimers.TryRemove(key, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                _logger.LogDebug("Cancelled typing auto-expire timer for user {UserId}, connection {ConnectionId}, conversation {ConversationId}", userId, connectionId, conversationId);
            }
        }

        private async Task RunAutoExpireAsync(string conversationId, string userId, string connectionId, CancellationToken ct)
        {
            try
            {
                await Task.Delay(TypingTtl, ct); // Bị cancel nếu StartTyping/StopTyping gọi lại
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Timer hết hạn một cách tự nhiên → auto-expire
            try
            {
                await AutoExpireTypingAsync(conversationId, userId, connectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while auto-expiring typing state for user {UserId}, connection {ConnectionId}, conversation {ConversationId}", userId, connectionId, conversationId);
            }
        }

        /// <summary>
        /// Tự động broadcast isTyping=false sau khi TTL hết (thay thế Hangfire job).
        /// </summary>
        private async Task AutoExpireTypingAsync(string conversationId, string userId, string connectionId)
        {
            // Dọn dẹp key khỏi dictionary (timer đã hoàn thành)
            var key = TimerKey(conversationId, userId, connectionId);
            if (_expireTimers.TryRemove(key, out var cts))
                cts.Dispose();

            // Xóa khỏi Redis SortedSet
            await _redis.RemoveUserTypingAsync(conversationId, userId, connectionId);

            // Multi-tab check: user có tab khác đang typing không?
            var hasOtherConnections = await _redis.HasOtherTypingConnectionsAsync(conversationId, userId, connectionId);
            if (hasOtherConnections)
                return; // User vẫn typing từ tab khác → không broadcast stop

            // Build payload đầy đủ
            var userInfo = await GetUserInfoAsync(userId);
            var payload = new TypingEventPayload
            {
                UserId = userId,
                DisplayName = userInfo.DisplayName,
                Username = userInfo.Username,
                AvatarUrl = userInfo.AvatarUrl,
                ConversationId = conversationId,
                IsTyping = false,
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients
                .Group($"conversation_{conversationId}")
                .SendAsync("UserTyping", payload);
            _logger.LogDebug("Auto-expired typing state for user {UserId}, connection {ConnectionId}, conversation {ConversationId}", userId, connectionId, conversationId);
        }

        private static string TimerKey(string conversationId, string userId, string connectionId)
            => $"{conversationId}|{userId}|{connectionId}";

        private async Task<(string? DisplayName, string? Username, string? AvatarUrl)> GetUserInfoAsync(string userId)
        {
            var avatarType = UserMediaType.Avatar.GetDescription();
            var result = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new
                {
                    u.DisplayName,
                    u.Username,
                    AvatarUrl = _dbContext.UserMedias
                        .Where(um => um.UserId == u.Id && um.IsPrimary && um.MediaType == avatarType)
                        .Select(um => um.MediaUrl)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync();

            return result == null
                ? (null, null, null)
                : (result.DisplayName, result.Username, result.AvatarUrl);
        }
    }
}
