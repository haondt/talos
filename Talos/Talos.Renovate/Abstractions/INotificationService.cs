using Talos.Renovate.Models;

namespace Talos.Renovate.Abstractions
{
    public interface INotificationService
    {
        Task<string> CreateInteractionAsync(ScheduledPushWithIdentity push);
        Task DeleteInteraction(string id);
        Task Notify(ScheduledPushWithIdentity push);
        Task Notify(PipelineCompletionEvent pipelineCompleted);
    }
}
