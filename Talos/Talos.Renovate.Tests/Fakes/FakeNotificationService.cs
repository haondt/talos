using Talos.Renovate.Abstractions;
using Talos.Renovate.Models;

namespace Talos.Renovate.Tests.Fakes
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

        public Task Notify(PipelineCompletionEvent pipelineCompleted)
        {
            return Task.CompletedTask;
        }

        public Task Notify(ScheduledPushWithIdentity push)
        {
            return Task.CompletedTask;
        }
    }
}
