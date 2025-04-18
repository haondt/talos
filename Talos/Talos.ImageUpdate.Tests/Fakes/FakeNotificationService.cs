using Talos.ImageUpdate.UpdatePushing.Models;
using Talos.ImageUpdate.UpdatePushing.Services;

namespace Talos.ImageUpdate.Tests.Fakes
{
    internal class FakeNotificationService : INotificationService
    {
        public Task<string> CreateInteractionAsync(ScheduledPushWithIdentity push)
        {
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        public Task DeleteInteraction(string id)
        {
            return Task.CompletedTask;
        }


        public Task Notify(ScheduledPushWithIdentity push)
        {
            return Task.CompletedTask;
        }
    }
}
