using Talos.Renovate.Abstractions;

namespace Talos.Renovate.Tests.Fakes
{
    internal class FakeNotificationService : INotificationService
    {
        public Task DeleteNotification(string id)
        {
            return Task.CompletedTask;
        }
    }
}
