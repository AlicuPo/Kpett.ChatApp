using Kpett.ChatApp.Hubs;
using Kpett.ChatApp.Receive;
using Microsoft.AspNetCore.SignalR;

namespace Kpett.ChatApp.Services.Impls
{
    public class RealtimeService : IRealtimeService
    {
        private readonly IHubContext<ChatHub> _hubContext;
        public RealtimeService(IHubContext<ChatHub> hubContext)
        {
            _hubContext = hubContext;
        }
        public async Task PublishToGroupAsync(string groupName, string method, object data)
        {
            await _hubContext.Clients.Group(groupName).SendAsync(method, data);
        }
        public async Task PublishAsync(string topic, object data)
        {
            var parts = topic.Split(':');
            if (parts.Length >= 2 && parts[0] == "user")
            {
                var userId = parts[1];
                await _hubContext.Clients.User(userId).SendAsync("Notification", data);
            }
        }

    }
}
