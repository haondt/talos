using Talos.Renovate.Models;

namespace Talos.Renovate.Abstractions
{
    public interface IPushQueueMutator
    {
        Task AcquireQueueLock();
        Task<long> ClearDeadLettersAsync();
        Task<long> GetDeadLetterQueueSizeAsync();
        void ReleaseQueueLock();
        Task<long> ReplayDeadLettersAsync();
        Task UpsertAndEnqueuePushAsync(ScheduledPushWithIdentity push);
    }
}