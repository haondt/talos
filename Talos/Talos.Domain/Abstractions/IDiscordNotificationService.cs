using Discord.WebSocket;
using Talos.Domain.Services;

namespace Talos.Domain.Abstractions
{
    public interface IDiscordNotificationService : ITalosNotificationService
    {
        Task CompleteInteractionAsync(string id, SocketInteraction interaction);
    }
}
