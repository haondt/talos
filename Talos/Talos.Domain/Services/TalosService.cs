// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Talos.Discord.Models;

namespace Talos.Domain.Services;

public class TalosService : IHostedService
{
    private readonly IDiscordBot _bot;

    public TalosService(
        IDiscordBot bot,
        IOptions<DiscordSettings> discordSettings)
    {
        _bot = bot;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return _bot.StartAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _bot.StopAsync();
    }
}
