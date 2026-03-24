using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;

namespace Kpett.ChatApp.Tests.Infrastructure;

public static class TestData
{
    public static User CreateUser(string id, string email)
    {
        return new User
        {
            Id = id,
            Name = id,
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            DisplayName = id,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            Status = UserStatusEnums.Offline.GetDescription()
        };
    }

    public static Conversation CreateConversation(string id)
    {
        return new Conversation
        {
            Id = id,
            Type = "direct",
            Name = $"Conversation {id}",
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    public static ConversationParticipant CreateConversationParticipant(string id, string conversationId, string userId)
    {
        return new ConversationParticipant
        {
            Id = id,
            ConversationId = conversationId,
            UserId = userId,
            JoinedAt = DateTime.UtcNow,
            LastReadAt = DateTime.UtcNow
        };
    }

    public static Post CreatePost(long id, string userId, string content = "hello post")
    {
        return new Post
        {
            Id = id,
            CreatedByUserId = userId,
            Content = content,
            Privacy = "Public",
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }

    public static FriendRequest CreatePendingFriendRequest(string id, string senderId, string receiverId)
    {
        return new FriendRequest
        {
            Id = id,
            SenderId = senderId,
            ReceiverId = receiverId,
            Status = FriendshipsEnums.Pending.GetDescription(),
            CreatedAt = DateTime.UtcNow
        };
    }
}
