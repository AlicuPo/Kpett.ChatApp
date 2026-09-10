namespace Kpett.ChatApp.Services.Abstractions
{
    public interface IAutoPostService
    {
        /// <summary>
        /// Fetch RSS và tự tạo post. Được gọi bởi Hangfire recurring job.
        /// </summary>
        Task<int> FetchAndPostAsync(CancellationToken cancel = default);

        /// <summary>
        /// Trigger thủ công (cho admin endpoint hoặc test).
        /// </summary>
        Task<int> FetchAndPostOnceAsync(CancellationToken cancel = default);
    }
}
