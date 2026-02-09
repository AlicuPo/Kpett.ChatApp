namespace Kpett.ChatApp.Receive
{
    public interface IRealtimeService
    {
        Task PublishToGroupAsync(string groupName, string method, object data);
        Task PublishAsync(string topic, object data);
    }

}
