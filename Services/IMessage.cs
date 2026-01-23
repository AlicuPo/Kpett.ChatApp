using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Services
{
    public interface IMessage
    {
        Task<MessageRespone> GetMessages(MessageRequest message, SearchRequest search, CancellationToken cancel);
    }
}
