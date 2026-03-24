namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IConversationAccessService
    {
        Task EnsureCanAccessConversationAsync(string conversationId, string userId, CancellationToken cancellationToken);
    }
}
