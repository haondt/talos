using Talos.Renovate.Models;

namespace Talos.Renovate.Abstractions
{
    public interface INotificationService
    {
        Task<string> CreateInteractionAsync(ImageUpdateIdentity id, ImageUpdate update);
        Task DeleteInteraction(string id);
        Task Notify(ImageUpdateIdentity id, ImageUpdate update);
        Task Notify(PipelineCompletionEvent pipelineCompleted);
    }
}
