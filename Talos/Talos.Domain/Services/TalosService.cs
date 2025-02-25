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

    public TalosService(
        IDiscordBot discordBot,
        IImageUpdaterService imageUpdater,
        IOptions<DiscordSettings> discordSettings)
    {
        _discordBot = discordBot;
        _imageUpdater = imageUpdater;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        //await _bot.StartAsync();
        await _imageUpdater.RunAsync();



    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // await _bot.StopAsync();
        return Task.CompletedTask;
    }
}
