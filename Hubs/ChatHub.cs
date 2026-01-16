using Microsoft.AspNetCore.SignalR;
using Kpett.ChatApp.DTOs;
namespace Kpett.ChatApp.Hubs
{
    public class ChatHub : Hub
    {
        public async Task SendMessage(ChatMessageDto data)
        {
            await Clients.All.SendAsync("ReceiveMessage", data);
        }
    }
}
