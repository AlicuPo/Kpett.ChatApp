using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
namespace Kpett.ChatApp.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IDatabase _redis; // Redis
        private readonly IMessage _messageService;

        public ChatHub(IConnectionMultiplexer redis, IMessage messageService)
        {
            _redis = redis.GetDatabase();
            _messageService = messageService;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;

            await base.OnConnectedAsync();
        }

        // Gửi tin nhắn Realtime
        public async Task SendMessage(string conversationId, SendMessageRequest request, CancellationToken cancel)
        {
            var userId = Context.UserIdentifier ?? "unknow";
            var messageDto = await _messageService.SendMessageAsync(conversationId, userId, request, cancel);
            await Clients.Group(conversationId).SendAsync("ReceiveMessage", messageDto);
        }

        // Typing Indicator
        public async Task SendTyping(string conversationId, bool isTyping)
        {
            var userId = Context.UserIdentifier;
            await Clients.OthersInGroup(conversationId).SendAsync("UserTyping", new { userId, isTyping });
        }
    }
}
