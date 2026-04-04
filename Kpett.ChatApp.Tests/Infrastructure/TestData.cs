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
            Username = id,
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

    public static ConversationKey CreateConversationKey(string conversationId, string userAId, string userBId)
    {
        var (userLowId, userHighId) = NormalizePair(userAId, userBId);

        return new ConversationKey
        {
            Id = Guid.NewGuid().ToString(),
            ConversationId = conversationId,
            UserLowId = userLowId,
            UserHighId = userHighId
        };
    }

    public static Post CreatePost(string id, string userId, string content = "hello post")
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

    public static FriendRequest CreateFriendRequest(
        string id,
        string senderId,
        string receiverId,
        string status,
        DateTime? createdAt = null,
        DateTime? updatedAt = null)
    {
        var (userLowId, userHighId) = NormalizePair(senderId, receiverId);

        return new FriendRequest
        {
            Id = id,
            UserLowId = userLowId,
            UserHighId = userHighId,
            SenderId = senderId,
            ReceiverId = receiverId,
            Status = status,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            UpdatedAt = updatedAt
        };
    }

    public static FriendRequest CreatePendingFriendRequest(string id, string senderId, string receiverId, DateTime? createdAt = null, DateTime? updatedAt = null)
    {
        return CreateFriendRequest(
            id,
            senderId,
            receiverId,
            FriendshipsEnums.Pending.GetDescription(),
            createdAt,
            updatedAt);
    }

    public static Friendship CreateAcceptedFriendship(string userAId, string userBId, DateTime? createdAt = null)
    {
        var userLowId = string.CompareOrdinal(userAId, userBId) < 0 ? userAId : userBId;
        var userHighId = string.CompareOrdinal(userAId, userBId) < 0 ? userBId : userAId;

        return new Friendship
        {
            UserLowId = userLowId,
            UserHighId = userHighId,
            Status = FriendshipsEnums.Accepted.GetDescription(),
            ActionUserId = userAId,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
    }

    private static (string LowId, string HighId) NormalizePair(string firstUserId, string secondUserId)
    {
        return string.CompareOrdinal(firstUserId, secondUserId) < 0
            ? (firstUserId, secondUserId)
            : (secondUserId, firstUserId);
    }
}
