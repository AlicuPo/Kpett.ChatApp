namespace Kpett.ChatApp.Receive
{
    public interface IRealtimeService
    {
        Task PublishAsync(string channel, object data);
    }

}
