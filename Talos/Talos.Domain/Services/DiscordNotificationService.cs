using Talos.Renovate.Abstractions;
using Talos.Renovate.Models;

namespace Talos.Domain.Services
{
    public class DiscordNotificationService : INotificationService
    {
        public async Task<string> CreateInteraction(ImageUpdate update)
        {
            return Guid.NewGuid().ToString();
        }

        public Task DeleteInteraction(string id)
        {
            return Task.CompletedTask;
        }

        public Task Notify(ImageUpdate update)
        {
            return Task.CompletedTask;
        }

    }
}
