// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Talos.Discord.Models;
using Talos.Renovate.Abstractions;

namespace Talos.Domain.Services;

public class TalosService : IHostedService
{
    private readonly IDiscordBot _discordBot;
    private readonly IImageUpdaterService _imageUpdater;
    private readonly IUpdateThrottlingQueueConsumer _updateQueue;

    public TalosService(
        IDiscordBot discordBot,
        IImageUpdaterService imageUpdater,
        IUpdateThrottlingQueueConsumer updateQueue,
        IOptions<DiscordSettings> discordSettings)
    {
        _discordBot = discordBot;
        _imageUpdater = imageUpdater;
        _updateQueue = updateQueue;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        //await _discordBot.StartAsync();
        _ = _updateQueue.RunAsync(cancellationToken);
        _ = _imageUpdater.RunAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // await _discordBot.StopAsync();
        return Task.CompletedTask;
    }
}
