using Kpett.ChatApp.DTOs;
using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace Kpett.ChatApp.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly Services.Interfaces.IRedisService _redis;
        private readonly IMessageService _messageService;

        public ChatHub(Services.Interfaces.IRedisService redis, IMessageService messageService)
        {
            _redis = redis;
            _messageService = messageService;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
            {
                await base.OnConnectedAsync();
                return;
            }

            try
            {
                await _redis.AddConnectionAsync(userId, Context.ConnectionId);

                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Connection error: {ex.Message}");
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;

            if (!string.IsNullOrEmpty(userId))
            {
                try
                {
                    // Remove connection from Redis
                    await _redis.RemoveConnectionAsync(userId, Context.ConnectionId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Disconnect error: {ex.Message}");
                }
            }

            await base.OnDisconnectedAsync(exception);
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

            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);

                // Store in Redis
                await _redis.AddUserToConversationAsync(conversationId, userId);

                // Notify others that user joined
                await Clients.OthersInGroup(conversationId).SendAsync("UserJoined", new
                {
                    userId,
                    conversationId,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", $"Failed to join conversation: {ex.Message}");
            }
        }

        /// <summary>
        /// Leave a conversation group
        /// </summary>
        public async Task LeaveConversation(string conversationId)
        {
            var userId = Context.UserIdentifier;

            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId);

                // Remove from Redis
                await _redis.RemoveUserFromConversationAsync(conversationId, userId);

                // Notify others that user left
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
                await _messageService.SendMessageAsync(conversationId, userId, request, cancel);

                // Create message DTO for broadcast
                var messageDTO = new MessageDTO
                {
                    SenderId = userId,
                    Content = request.Content,
                    Type = request.Type ?? "text",
                    Metadata = request.Metadata,
                    CreatedAt = DateTime.UtcNow
                };

                // Broadcast to all users in the conversation
                await Clients.Group(conversationId).SendAsync("ReceiveMessage", new
                {
                    conversationId,
                    message = messageDTO,
                    clientMessageId = request.ClientMessageId,
                    timestamp = DateTime.UtcNow
                });

                // Send confirmation to sender
                await Clients.Caller.SendAsync("MessageSent", new
                {
                    clientMessageId = request.ClientMessageId,
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

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(conversationId))
                return;

            try
            {
                await Clients.OthersInGroup(conversationId).SendAsync("UserTyping", new
                {
                    userId,
                    conversationId,
                    isTyping,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Typing indicator error: {ex.Message}");
            }
        }
    }
}
