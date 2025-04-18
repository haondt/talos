using Talos.ImageUpdate.UpdatePushing.Models;

namespace Talos.ImageUpdate.UpdatePushing.Services
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