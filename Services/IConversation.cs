using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Services
{
    public interface IConversation
    {
        //Task<List<ConversationResponse>> GetConversationList(SearchRequest search, CancellationToken cancel);
        Task<ConversationResponse> CreateConversaTion(ConversationKeysRequest request, CancellationToken cancel);
    }
    public class ConversationImpl : IConversation
    {
        private readonly AppDbContext _dbContext;
        public ConversationImpl(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<ConversationResponse> CreateConversaTion(ConversationKeysRequest request, CancellationToken cancel)
        {
            if (request == null)
                throw new AppException(StatusCodes.Status400BadRequest, "Request cannot be null");

            string _id = Guid.NewGuid().ToString();
            var newconversation = new Conversation
            {
                Id = _id,
                Name = request.Name,
                AvatarUrl = request.AvatarUrl,
                Type = request.Type,
                LastMessageAt = DateTime.UtcNow,

            };
            await _dbContext.Conversations.AddAsync(newconversation, cancel);
            var newconversationKeys = new ConversationKey
            {
                Id = _id,
                ConversationId = newconversation.Id,
                UserLowId = request.UserLow,
                UserHighId = request.UserHigh
            };
            await _dbContext.ConversationKeys.AddAsync(newconversationKeys, cancel);
        
            await _dbContext.SaveChangesAsync(cancel);

            return new ConversationResponse
            {
                Id = newconversation.Id,
                Name = request.Name,
                AvatarUrl = request.AvatarUrl,
                Type = request.Type,
                LastMessageAt = DateTime.UtcNow,

            };




        }

    }
}
