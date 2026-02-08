using Kpett.ChatApp.DTOs;
using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Kpett.ChatApp.Services.Impls
{
    public class ConversationService 
    {
        //private readonly AppDbContext _dbcontext;
        //private readonly IToken _token;
        //public ConversationRespository(AppDbContext dbContext, IToken token)
        //{
        //    _dbcontext = dbContext;
        //    _token = token;
        //}
        //public async Task<List<ConversationResponse>> GetConversationList( SearchRequest search, CancellationToken cancel)
        //{
        //    var userClaims = _token.GetUserClaims();
        //    var userIdToken = userClaims?.UserId ?? string.Empty;

        //    var userName = userClaims?.Username ?? string.Empty;
        //    if (string.IsNullOrEmpty(userName))
        //    {
        //        userName = "Unknown";
        //    }

        //    // Lấy danh sách Conversations mà User tham gia
        //    var query = from p in _dbcontext.ConversationParticipants.AsNoTracking()
        //                where p.UserId == userIdToken && (p.IsArchived == null || p.IsArchived == false)
        //                join c in _dbcontext.Conversations.AsNoTracking() on p.ConversationId equals c.Id

        //                // Lấy tin nhắn cuối cùng bằng Subquery
        //                let lastMessage = _dbcontext.Messages
        //                    .AsNoTracking()
        //                    .Where(m => m.ConversationId == c.Id)
        //                    .OrderByDescending(m => m.Id)
        //                   .FirstOrDefault()
        //                // Đếm số tin chưa đọc bằng Subquery
        //                let unreadCount = _dbcontext.Messages
        //                    .AsNoTracking()
        //                    .Where(m => m.ConversationId == c.Id
        //                           && m.SenderId != userIdToken
        //                           && m.Id > (p.LastReadMessageId ?? 0))
        //                    .Count()

        //                orderby c.LastMessageAt descending // Sắp xếp theo hoạt động mới nhất 
        //                select new ConversationResponse
        //                {
        //                    Id = c.Id,
        //                    Name = c.Name,
        //                    AvatarUrl = c.AvatarUrl,
        //                    Type = c.Type,
        //                    LastMessageAt = c.LastMessageAt,
        //                    UnreadCount = unreadCount,
        //                    LastMessage = lastMessage != null ? new LastMessageDto
        //                    {
        //                        Content = lastMessage.Metadata,
        //                        SenderId = lastMessage.SenderId,
        //                        CreatedAt = lastMessage.CreatedAt
        //                    } : null
        //                };

        //    return await query
        //        .Skip((search.Page - 1) * search.PageSize)
        //        .Take(search.PageSize)
        //        .ToListAsync(cancel);
        //}
    }
}
