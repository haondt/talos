using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Talos.Discord.Models;

namespace Talos.Discord.Services
{
    public class ExternalDiscordEventChannelListener(
        IOptions<DiscordSettings> discordSettings,
        DiscordSocketClient discordClient,
        DiscordClientState clientState, ILogger<ExternalDiscordEventChannelListener> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await clientState.StartTask;
            using var combinedTokenCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, clientState.CancellationToken);
            await Task.WhenAll(
                WatchNotificationChannelAsync(combinedTokenCts.Token),
                WatchInteractionChannelAsync(combinedTokenCts.Token));
        }

        private async Task WatchNotificationChannelAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var next = await ExternalDiscordEventChannelProvider.NotificationChannel.Reader.ReadAsync(stoppingToken);
                    await HandleNotificationEvent(next);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while listening to notification channel. Waiting 1 second before attempting to reconnect.");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }


        private async Task WatchInteractionChannelAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var next = await ExternalDiscordEventChannelProvider.InteractionChannel.Reader.ReadAsync(stoppingToken);
                    //await HandleInteractionEvent(next);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while listening to interaction channel. Waiting 1 second before attempting to reconnect.");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        private async Task HandleNotificationEvent(ExternalDiscordNotification next)
        {
            var channelId = discordSettings.Value.ChannelId;
            var channel = discordClient.GetChannel(channelId) as ISocketMessageChannel;
            if (channel == null)
            {
                logger.LogError($"Failed to retrieve channel {channelId}.");
                return;
            }

            await channel.SendMessageAsync(embed: next.Embed);
        }

    }
}
