using Talos.ImageUpdate.UpdatePushing.Models;

namespace Talos.ImageUpdate.UpdatePushing.Services
{
    public interface INotificationService
    {
        Task<string> CreateInteractionAsync(ScheduledPushWithIdentity push);
        Task DeleteInteraction(string id);
        Task Notify(ScheduledPushWithIdentity push);
    }
}
