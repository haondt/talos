using Talos.Renovate.Models;

namespace Talos.Renovate.Abstractions
{
    public interface INotificationService
    {
        Task<string> CreateInteraction(ImageUpdate update);
        Task DeleteInteraction(string id);
        Task Notify(ImageUpdate update);
        Task Notify(PipelineCompletionEvent pipelineCompleted);
    }
}
