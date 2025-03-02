using Discord.WebSocket;
using Talos.Renovate.Abstractions;

namespace Talos.Domain.Abstractions
{
    public interface IDiscordNotificationService : INotificationService
    {
        Task CompleteInteractionAsync(string id, SocketInteraction interaction);
    }
}
