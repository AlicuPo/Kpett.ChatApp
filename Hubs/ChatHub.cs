using Kpett.ChatApp.DTOs;
using Kpett.ChatApp.DTOs.Request.Message;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Kpett.ChatApp.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IRedisService _redis;
        private readonly IMessageService _messageService;
        private readonly IConversationAccessService _conversationAccessService;
        private readonly IConversationPresenceService _conversationPresenceService;

        public ChatHub(
            IRedisService redis,
            IMessageService messageService,
            IConversationAccessService conversationAccessService,
            IConversationPresenceService conversationPresenceService)
        {
            _redis = redis;
            _messageService = messageService;
            _conversationAccessService = conversationAccessService;
            _conversationPresenceService = conversationPresenceService;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;

            var wasOnline = await _redis.IsUserOnlineAsync(userId);
            await _redis.AddConnectionAsync(userId, Context.ConnectionId);

            if (!wasOnline)
            {
                await Clients.Group($"presence_watcher_{userId}").SendAsync("UserStatusChanged", new
                {
                    userId = userId,
                    isOnline = true
                });
            }

            await base.OnConnectedAsync();
        }


        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;

            await _redis.RemoveConnectionAsync(userId, Context.ConnectionId);
            var isStillOnline = await _redis.IsUserOnlineAsync(userId);

            if (!isStillOnline)
            {
                await Clients.Group($"presence_watcher_{userId}").SendAsync("UserStatusChanged", new
                {
                    userId = userId,
                    isOnline = false
                });
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Subscribe friend status change
        public async Task SubscribeToPresence(List<string> targetUserIds)
        {
            // Đưa ConnectionId của người này vào Group mang tên người họ muốn theo dõi
            foreach (var targetId in targetUserIds)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"presence_watcher_{targetId}");
            }
        }

        public async Task UnsubscribeFromPresence(List<string> targetUserIds)
        {
            foreach (var targetId in targetUserIds)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"presence_watcher_{targetId}");
            }
        }

        /// <summary>
        /// Join a user to a conversation group
        /// </summary>
        public async Task JoinConversation(string conversationId)
        {
            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
            {
                await Clients.Caller.SendAsync("Error", "User ID not found.");
                return;
            }

            if (string.IsNullOrWhiteSpace(conversationId))
            {
                await Clients.Caller.SendAsync("Error", "Conversation ID is required.");
                return;
            }

            var presenceTracked = false;
            var addedToGroup = false;

            try
            {
                await _conversationAccessService.EnsureCanAccessConversationAsync(conversationId, userId, Context.ConnectionAborted);

                await _conversationPresenceService.TrackConversationConnectionAsync(conversationId, userId, Context.ConnectionId);
                presenceTracked = true;

                await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);
                addedToGroup = true;

                await Clients.OthersInGroup(conversationId).SendAsync("UserJoined", new
                {
                    userId,
                    conversationId,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (AppException appEx)
            {
                if (presenceTracked)
                {
                    try
                    {
                        await _conversationPresenceService.UntrackConversationConnectionAsync(conversationId, userId, Context.ConnectionId);
                    }
                    catch
                    {
                    }
                }

                if (addedToGroup)
                {
                    try
                    {
                        await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId);
                    }
                    catch
                    {
                    }
                }

                await Clients.Caller.SendAsync("Error", appEx.Message);
            }
            catch (Exception ex)
            {
                if (presenceTracked)
                {
                    try
                    {
                        await _conversationPresenceService.UntrackConversationConnectionAsync(conversationId, userId, Context.ConnectionId);
                    }
                    catch
                    {
                    }
                }

                if (addedToGroup)
                {
                    try
                    {
                        await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId);
                    }
                    catch
                    {
                    }
                }

                await Clients.Caller.SendAsync("Error", $"Failed to join conversation: {ex.Message}");
            }
        }

        /// <summary>
        /// Leave a conversation group
        /// </summary>
        public async Task LeaveConversation(string conversationId)
        {
            var userId = Context.UserIdentifier;

            if (string.IsNullOrWhiteSpace(conversationId))
            {
                await Clients.Caller.SendAsync("Error", "Conversation ID is required.");
                return;
            }

            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId);

                if (string.IsNullOrEmpty(userId))
                {
                    return;
                }

                await _conversationPresenceService.UntrackConversationConnectionAsync(conversationId, userId, Context.ConnectionId);

                await Clients.OthersInGroup(conversationId).SendAsync("UserLeft", new
                {
                    userId,
                    conversationId,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Failed to leave conversation: {ex.Message}");
            }
        }

        /// <summary>
        /// Send a message in real-time
        /// </summary>
        public async Task SendMessage(string conversationId, SendMessageRequest request, CancellationToken cancel)
        {
            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
            {
                await Clients.Caller.SendAsync("Error", "User ID not found.");
                return;
            }

            if (string.IsNullOrEmpty(conversationId))
            {
                await Clients.Caller.SendAsync("Error", "Conversation ID is required.");
                return;
            }

            try
            {
                // Send message to database
                var message = await _messageService.SendMessageAsync(conversationId, userId, request, cancel);

                // Create message DTO for broadcast but DO NOT broadcast here manually
                // _messageService.SendMessageAsync already handles broadcasting via RealtimeService

                // Send confirmation to sender
                await Clients.Caller.SendAsync("MessageSent", new
                {
                    clientMessageId = request.ClientMessageId,
                    messageId = message.Id,
                    success = true,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (AppException appEx)
            {
                await Clients.Caller.SendAsync("Error", appEx.Message);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Failed to send message: {ex.Message}");
            }
        }

        /// <summary>
        /// Send typing indicator
        /// </summary>
        public async Task SendTyping(string conversationId, bool isTyping)
        {
            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
            {
                await Clients.Caller.SendAsync("Error", "User ID not found.");
                return;
            }

            if (string.IsNullOrWhiteSpace(conversationId))
            {
                await Clients.Caller.SendAsync("Error", "Conversation ID is required.");
                return;
            }

            try
            {
                await _conversationAccessService.EnsureCanAccessConversationAsync(conversationId, userId, Context.ConnectionAborted);

                await Clients.OthersInGroup(conversationId).SendAsync("UserTyping", new
                {
                    userId,
                    conversationId,
                    isTyping,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (AppException appEx)
            {
                await Clients.Caller.SendAsync("Error", appEx.Message);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Typing indicator error: {ex.Message}");
            }
        }
    }
}
