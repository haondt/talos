// See https://aka.ms/new-console-template for more information
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Talos.Discord.Abstractions;
using Talos.Discord.Extensions;
using Talos.Discord.Models;

namespace Talos.Discord.Services;

public class DiscordBot : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly DiscordClientState _clientState;
    private readonly IInteractionServiceHandler _interactionService;
    private readonly ILogger<DiscordBot> _logger;
    private readonly DiscordSettings _settings;

    public DiscordBot(
        DiscordSocketClient client,
        DiscordClientState clientState,
        IOptions<DiscordSettings> discordSettings,
        IInteractionServiceHandler interactionService,
        ILogger<DiscordBot> logger)
    {
        _client = client;
        _clientState = clientState;
        _interactionService = interactionService;
        _logger = logger;
        _settings = discordSettings.Value;

        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
    }

    private async Task ReadyAsync()
    {
        _logger.LogInformation("Ready");
        _logger.LogInformation("Syncing commands...");
        await _interactionService.OnReadyAsync();
        _logger.LogInformation("Command list synced.");

        _clientState.SignalStart();
    }

    private Task LogAsync(LogMessage logMessage)
    {
        logMessage.LogTo(_logger);
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {

            await _client.LoginAsync(TokenType.Bot, _settings.BotToken);
            await _client.StartAsync();
        }
        catch
        {
            await _clientState.SignalShutdown();
            throw;
        }

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        finally
        {
            await _clientState.SignalShutdown();
            await _client.StopAsync();
            await _client.LogoutAsync();
        }

    }
}
