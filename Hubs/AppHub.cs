using Kpett.ChatApp.DTOs.Response.Conversation;
using Kpett.ChatApp.Helpers;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Kpett.ChatApp.Hubs
{
    [Authorize]
    public class AppHub : Hub
    {
        private readonly IRedisService _redisService;
        private readonly IConversationTypingService _typingService;
        private readonly IConversationAccessService _conversationAccessService;

        public AppHub(
            IRedisService redisService,
            IConversationTypingService typingService,
            IConversationAccessService conversationAccessService)
        {
            _redisService = redisService;
            _typingService = typingService;
            _conversationAccessService = conversationAccessService;
        }

        // LIFECYCLE
        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;

            var wasOnline = await _redisService.IsUserOnlineAsync(userId);
            await _redisService.AddConnectionAsync(userId, Context.ConnectionId);

            if (!wasOnline)
            {
                await Clients.Group($"presence_watcher_{userId}").SendAsync("UserStatusChanged", new
                {
                    userId,
                    isOnline = true
                });
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;

            // 1. Presence: cập nhật online status
            await _redisService.RemoveConnectionAsync(userId, Context.ConnectionId);
            var isStillOnline = await _redisService.IsUserOnlineAsync(userId);

            if (!isStillOnline)
            {
                await Clients.Group($"presence_watcher_{userId}").SendAsync("UserStatusChanged", new
                {
                    userId,
                    isOnline = false
                });
            }

            // 2. Typing cleanup: broadcast stop cho mọi conversation user đang typing
            try
            {
                var stoppedTyping = await _typingService.CleanupConnectionTypingAsync(Context.ConnectionId);
                foreach (var (conversationId, typingUserId) in stoppedTyping)
                {
                    // Chỉ broadcast nếu user không có tab khác đang typing
                    var hasOtherConnections = await _redisService.HasOtherTypingConnectionsAsync(conversationId, typingUserId, Context.ConnectionId);

                    if (!hasOtherConnections)
                    {
                        var payload = new TypingEventPayload
                        {
                            UserId = typingUserId,
                            ConversationId = conversationId,
                            IsTyping = false,
                            Timestamp = DateTime.UtcNow
                        };
                        await Clients.Group($"conversation_{conversationId}")
                            .SendAsync("UserTyping", payload);
                    }
                }
            }
            catch
            {
                // Không để lỗi cleanup ảnh hưởng đến disconnect lifecycle
            }

            // 3. Remove khỏi tất cả SignalR conversation groups
            var conversations = await _redisService.GetConnectionConversationsAsync(Context.ConnectionId);
            foreach (var convId in conversations)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation_{convId}");
            }
            await _redisService.ClearConnectionConversationsAsync(Context.ConnectionId);

            await base.OnDisconnectedAsync(exception);
        }

        // =====================================================================
        // PRESENCE (Friend status)
        // =====================================================================

        public async Task SubscribeToPresence(List<string> targetUserIds)
        {
            foreach (var targetId in targetUserIds)
                await Groups.AddToGroupAsync(Context.ConnectionId, $"presence_watcher_{targetId}");
        }

        public async Task UnsubscribeFromPresence(List<string> targetUserIds)
        {
            foreach (var targetId in targetUserIds)
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"presence_watcher_{targetId}");
        }

        // CONVERSATION (Join / Leave)
        /// <summary>
        /// Client gọi khi mở màn hình chat của 1 conversation.
        /// Kiểm tra quyền, thêm vào SignalR Group và track trong Redis.
        /// </summary>
        public async Task JoinConversation(string conversationId)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId))
            {
                await Clients.Caller.SendAsync("Error", "Unauthorized.");
                return;
            }

            try
            {
                // Kiểm tra quyền truy cập (có cache Redis 5 phút)
                var isCached = await _redisService.GetConversationAccessCacheAsync(userId, conversationId);
                if (!isCached)
                {
                    await _conversationAccessService.EnsureCanAccessConversationAsync(conversationId, userId, Context.ConnectionAborted);
                    await _redisService.SetConversationAccessCacheAsync(userId, conversationId, TimeSpan.FromMinutes(5));
                }

                // Tham gia SignalR Group
                await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");

                // Track connection → conversation trong Redis (dùng cho cleanup khi disconnect)
                await _redisService.TrackConnectionConversationAsync(Context.ConnectionId, conversationId);

                await Clients.Caller.SendAsync("JoinedConversation", new { conversationId });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", ex.Message);
            }
        }

        /// <summary>
        /// Client gọi khi đóng màn hình chat của 1 conversation.
        /// </summary>
        public async Task LeaveConversation(string conversationId)
        {
            var userId = Context.UserIdentifier;

            // Dừng typing (nếu đang gõ) trước khi rời phòng
            if (!string.IsNullOrEmpty(userId))
            {
                var shouldBroadcastStop = await _typingService.StopTypingAsync(conversationId, userId, Context.ConnectionId, Context.ConnectionAborted);

                if (shouldBroadcastStop)
                {
                    var payload = new TypingEventPayload
                    {
                        UserId = userId,
                        ConversationId = conversationId,
                        IsTyping = false,
                        Timestamp = DateTime.UtcNow
                    };
                    await Clients.Group($"conversation_{conversationId}").SendAsync("UserTyping", payload);
                }
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
            await _redisService.UntrackConnectionConversationAsync(Context.ConnectionId, conversationId);
            await Clients.Caller.SendAsync("LeftConversation", new { conversationId });
        }

        // TYPING
        /// <summary>
        /// Client gọi khi bắt đầu/tiếp tục gõ (isTyping=true) hoặc dừng gõ (isTyping=false).
        /// - Server tự động broadcast stop sau 5 giây nếu không nhận thêm event (server-side debounce).
        /// - Hỗ trợ multi-tab: mỗi connectionId là 1 session typing riêng.
        /// - Access check được cache trong Redis 5 phút.
        /// </summary>
        public async Task SendTyping(string conversationId, TypingEventPayload userTypingPayload, bool isTyping)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId))
            {
                await Clients.Caller.SendAsync("Error", "Unauthorized.");
                return;
            }

            try
            {
                // Access check với Redis cache (tránh DB hit mỗi lần gõ phím)
                var isCached = await _redisService.GetConversationAccessCacheAsync(userId, conversationId);
                if (!isCached)
                {
                    await _conversationAccessService.EnsureCanAccessConversationAsync(conversationId, userId, Context.ConnectionAborted);
                    await _redisService.SetConversationAccessCacheAsync(userId, conversationId, TimeSpan.FromMinutes(5));
                }

                if (isTyping)
                {
                    // StartTyping: returns true nếu cần broadcast (chưa typing từ bất kỳ tab nào)
                    var shouldBroadcast = await _typingService.StartTypingAsync(conversationId, userId, Context.ConnectionId, Context.ConnectionAborted);

                    if (shouldBroadcast)
                    {
                        // Query user info đầy đủ cho payload (chỉ query 1 lần khi bắt đầu)
                        var payload = new TypingEventPayload
                        {
                            UserId = userId,
                            DisplayName = userTypingPayload.DisplayName,
                            Username = userTypingPayload.Username,
                            AvatarUrl = userTypingPayload.AvatarUrl,
                            ConversationId = conversationId,
                            IsTyping = true,
                            Timestamp = DateTime.UtcNow
                        };
                        // Broadcast đến tất cả OTHERS trong conversation group
                        await Clients.OthersInGroup($"conversation_{conversationId}")
                            .SendAsync("UserTyping", payload);
                    }
                }
                else
                {
                    // StopTyping: returns true nếu không còn tab nào typing
                    var shouldBroadcast = await _typingService.StopTypingAsync(conversationId, userId, Context.ConnectionId, Context.ConnectionAborted);

                    if (shouldBroadcast)
                    {
                        var payload = new TypingEventPayload
                        {
                            UserId = userId,
                            ConversationId = conversationId,
                            IsTyping = false,
                            Timestamp = DateTime.UtcNow
                        };
                        await Clients.OthersInGroup($"conversation_{conversationId}")
                            .SendAsync("UserTyping", payload);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                await Clients.Caller.SendAsync("Error", ex.Message);
            }
        }
    }
}
