using Kpett.ChatApp.Hubs;
using Kpett.ChatApp.Receive;
using Microsoft.AspNetCore.SignalR;

namespace Kpett.ChatApp.Respository
{
    public class RealtimeRespository : IRealtimeService
    {
        private readonly IHubContext<ChatHub> _hubContext;
        public RealtimeRespository(IHubContext<ChatHub> hubContext)
        {
            _hubContext = hubContext;
        }
        public async Task PublishAsync(string channel, object data)
        { // Gửi dữ liệu đến tất cả client đang join channel (conversation)
          await _hubContext.Clients.Group(channel).SendAsync("ReceiveEvent", data); }
        }
}
