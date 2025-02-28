using Talos.Renovate.Models;

namespace Talos.Renovate.Abstractions
{
    public interface IPushQueueMutator
    {
        Task AcquireQueueLock();
        void ReleaseQueueLock();
        Task UpsertAndEnqueuePush(ScheduledPush push);
    }
}