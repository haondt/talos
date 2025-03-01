using System.Threading.Channels;
using Talos.Discord.Models;

namespace Talos.Discord.Services
{
    public static class ExternalDiscordEventChannelProvider
    {
        public static Channel<ExternalDiscordNotification> NotificationChannel { get; private set; }
        public static Channel<ExternalDiscordInteraction> InteractionChannel { get; private set; }

        static ExternalDiscordEventChannelProvider()
        {
            NotificationChannel = Channel.CreateUnbounded<ExternalDiscordNotification>(new()
            {
                SingleReader = true,
            });
            InteractionChannel = Channel.CreateUnbounded<ExternalDiscordInteraction>(new()
            {
                SingleReader = true,
            });
        }
    }
}
