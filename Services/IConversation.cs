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
        Task<List<ConversationResponse>> CreateConversaTion(CancellationToken cancel);
    }
    public class ConversationImpl : IConversation
    {
        private readonly AppDbContext _dbContext;
        public ConversationImpl(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<List<ConversationResponse>> CreateConversaTion(ConversationsRequest request, CancellationToken cancel)
        {
           if(request == null)        
                throw new AppException(StatusCodes.Status400BadRequest, "Request cannot be null");

           var newconversation = new ConversationResponse
           {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                AvatarUrl = request.AvatarUrl,
                Type = request.Type,
                LastMessageAt = DateTime.UtcNow,
                LastMessage = request.LastMessage != null ? new DTOs.LastMessageDto
                {
                    CreatedAt = DateTime.UtcNow,
                    SenderId = request.LastMessage.SenderId,
                    Content = request.LastMessage.Content,
                    
                } : null,
                UnreadCount = request.UnreadCount ?? 0
           };
          
              await _dbContext.Conversations.AddAsync(newconversation, cancel);
                await  _dbContext.SaveChangesAsync(cancel);


        }
      
    }
}
