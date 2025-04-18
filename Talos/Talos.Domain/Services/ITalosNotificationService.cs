using Talos.Domain.Models;
using Talos.ImageUpdate.UpdatePushing.Services;

namespace Talos.Domain.Services
{
    public interface ITalosNotificationService : INotificationService
    {
        Task Notify(PipelineCompletionEvent pipelineCompleted);
    }
}
